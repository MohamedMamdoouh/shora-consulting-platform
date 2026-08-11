using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Contracts.Availability;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class AvailabilityEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresFixture _sqlServer;
    private readonly string _databaseName;

    public AvailabilityEndpointTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
        var connectionString = sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        _databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database
            ?? throw new InvalidOperationException("Test database name is missing from the connection string.");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "Shora",
                    ["Jwt:Audience"] = "Shora.Web",
                    ["Jwt:SigningKey"] = "test-signing-key-min-32-characters-long!",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
                });
            });
            builder.ConfigureServices(_ => { });
        });
    }

    [Fact]
    public async Task Get_availability_returns_open_slots()
    {
        var client = _factory.CreateClient();
        var from = DateTime.UtcNow;
        var to = from.AddDays(14);
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Slots);
        Assert.All(body.Slots, slot => Assert.True(slot.EndTimeUtc > slot.StartTimeUtc));
    }

    [Fact]
    public async Task Get_availability_rejects_invalid_range()
    {
        var client = _factory.CreateClient();
        var value = DateTime.UtcNow.AddDays(1).ToString("O");
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(value)}&to={Uri.EscapeDataString(value)}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_availability_rejects_range_larger_than_horizon()
    {
        var client = _factory.CreateClient();
        var from = DateTime.UtcNow;
        var to = from.AddDays(29);
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_availability_excludes_blocked_slots()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await context.AvailabilitySlots.AsNoTracking().FirstAsync(slot => !slot.IsBooked);

        context.BlockedDates.Add(new BlockedDate
        {
            Id = Guid.NewGuid(),
            StartUtc = slot.StartTimeUtc,
            EndUtc = slot.EndTimeUtc,
            Reason = "test block"
        });
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        var from = slot.StartTimeUtc.AddDays(-1);
        var to = slot.EndTimeUtc.AddDays(1);
        var url = $"/api/v1/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body!.Slots, returned => returned.Id == slot.Id);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
    }
}

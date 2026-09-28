using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Services;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Postgres")]
public class SlotGenerationServiceTests
{
    private readonly PostgresFixture _sqlServer;

    public SlotGenerationServiceTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task GenerateHorizonAsync_materializes_future_unbooked_slots()
    {
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = TestServiceProviderFactory.Create(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services);
            await DatabaseSeeder.SeedAsync(services);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var slotGenerationService = scope.ServiceProvider.GetRequiredService<SlotGenerationService>();

            var initialCount = await context.AvailabilitySlots.CountAsync();
            Assert.True(initialCount > 0);

            await slotGenerationService.GenerateHorizonAsync();

            var afterSecondRunCount = await context.AvailabilitySlots.CountAsync();
            Assert.Equal(initialCount, afterSecondRunCount);
            Assert.All(await context.AvailabilitySlots.ToListAsync(), slot => Assert.False(slot.IsBooked));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task GenerateHorizonAsync_does_not_remove_booked_slots()
    {
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = TestServiceProviderFactory.Create(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services);
            await DatabaseSeeder.SeedAsync(services);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var slotGenerationService = scope.ServiceProvider.GetRequiredService<SlotGenerationService>();

            var bookedSlot = await context.AvailabilitySlots.FirstAsync();
            bookedSlot.IsBooked = true;
            await context.SaveChangesAsync();

            await slotGenerationService.GenerateHorizonAsync();

            var persistedBookedSlot = await context.AvailabilitySlots.SingleAsync(slot => slot.Id == bookedSlot.Id);
            Assert.True(persistedBookedSlot.IsBooked);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class RefreshTokenPurgeServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public RefreshTokenPurgeServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_deletes_only_expired_refresh_tokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            var (expiredTokenId, activeTokenId) = await SeedRefreshTokensAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var purgeService = scope.ServiceProvider.GetRequiredService<RefreshTokenPurgeService>();
            var deletedCount = await purgeService.RunAsync(cancellationToken);

            Assert.Equal(1, deletedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await context.RefreshTokens.AnyAsync(t => t.Id == expiredTokenId, cancellationToken));
            Assert.True(await context.RefreshTokens.AnyAsync(t => t.Id == activeTokenId, cancellationToken));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<(Guid ExpiredTokenId, Guid ActiveTokenId)> SeedRefreshTokensAsync(
        IServiceProvider services,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "token-user@test.local",
            Email = "token-user@test.local",
            EmailConfirmed = true,
            DisplayName = "Token User",
            Role = UserRole.Client
        };

        var createResult = await userManager.CreateAsync(user, "Password123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var expiredTokenId = Guid.NewGuid();
        var activeTokenId = Guid.NewGuid();

        context.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = expiredTokenId,
                UserId = userId,
                TokenHash = "expired-hash",
                CreatedAtUtc = now.AddDays(-10),
                ExpiresAtUtc = now.AddMinutes(-1)
            },
            new RefreshToken
            {
                Id = activeTokenId,
                UserId = userId,
                TokenHash = "active-hash",
                CreatedAtUtc = now.AddDays(-1),
                ExpiresAtUtc = now.AddDays(6)
            });

        await context.SaveChangesAsync(cancellationToken);
        return (expiredTokenId, activeTokenId);
    }

    private static IServiceProvider CreateServices(string connectionString, DateTime utcNow)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddScoped<RefreshTokenPurgeService>();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Domain.Constants;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class DatabaseSeederTests
{
    private readonly SqlServerFixture _sqlServer;

    public DatabaseSeederTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task SeedAsync_creates_roles_settings_and_admin_user()
    {
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = TestServiceProviderFactory.Create(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services);
            await DatabaseSeeder.SeedAsync(services);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            Assert.True(await roleManager.RoleExistsAsync(DatabaseSeeder.ClientRole));
            Assert.True(await roleManager.RoleExistsAsync(DatabaseSeeder.AdminRole));

            var settings = await context.Settings.SingleAsync();
            Assert.Equal(Settings.SingletonId, settings.Id);
            Assert.Equal(SettingsDefaults.SessionPrice, settings.SessionPrice);
            Assert.Equal(SettingsDefaults.SessionDurationMinutes, settings.SessionDurationMinutes);

            var admin = await userManager.FindByEmailAsync("admin@test.local");
            Assert.NotNull(admin);
            Assert.True(await userManager.IsInRoleAsync(admin, DatabaseSeeder.AdminRole));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = TestServiceProviderFactory.Create(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services);
            await DatabaseSeeder.SeedAsync(services);
            await DatabaseSeeder.SeedAsync(services);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Single(await context.Settings.ToListAsync());
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }
}

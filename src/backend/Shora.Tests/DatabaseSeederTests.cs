using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Domain.Entities;
using Shora.Infrastructure.Persistence;
using Shora.Tests.Support;

namespace Shora.Tests;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_creates_roles_settings_and_admin_user()
    {
        var services = TestServiceProviderFactory.Create(Guid.NewGuid().ToString());

        await DatabaseSeeder.SeedAsync(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roleManager.RoleExistsAsync(DatabaseSeeder.ClientRole));
        Assert.True(await roleManager.RoleExistsAsync(DatabaseSeeder.AdminRole));

        var settings = await context.Settings.SingleAsync();
        Assert.Equal(Settings.SingletonId, settings.Id);
        Assert.Equal(500m, settings.SessionPrice);
        Assert.Equal(60, settings.SessionDurationMinutes);

        var admin = await userManager.FindByEmailAsync("admin@test.local");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin, DatabaseSeeder.AdminRole));
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        var services = TestServiceProviderFactory.Create(Guid.NewGuid().ToString());

        await DatabaseSeeder.SeedAsync(services);
        await DatabaseSeeder.SeedAsync(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Single(await context.Settings.ToListAsync());
    }
}

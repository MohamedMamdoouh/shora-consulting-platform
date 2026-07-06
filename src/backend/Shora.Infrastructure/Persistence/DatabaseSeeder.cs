using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Persistence;

namespace Shora.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public const string ClientRole = "Client";
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await EnsureRolesAsync(roleManager, logger, cancellationToken);
        await EnsureSettingsAsync(context, configuration, logger, cancellationToken);
        await EnsureAdminUserAsync(userManager, configuration, logger, cancellationToken);
    }

    private static async Task EnsureRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in new[] { ClientRole, AdminRole })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded)
            {
                logger.LogWarning("Failed to create role {Role}: {Errors}", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task EnsureSettingsAsync(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await context.Settings.AnyAsync(s => s.Id == Settings.SingletonId, cancellationToken))
        {
            return;
        }

        context.Settings.Add(new Settings
        {
            Id = Settings.SingletonId,
            SessionPrice = 500m,
            SessionDurationMinutes = 60,
            BufferMinutes = 15,
            ReceiptUploadWindowMinutes = 60,
            CancellationRequestAutoDeclineHours = 1,
            ReceiptRetentionMonths = 24,
            ConsultantWhatsAppNumber = configuration["Seed:ConsultantWhatsAppNumber"] ?? "+201000000000",
            VodafoneCashNumber = configuration["Seed:VodafoneCashNumber"] ?? "01000000000",
            InstaPayHandle = configuration["Seed:InstaPayHandle"] ?? "consultant@instapay",
            PaymentInstructions = configuration["Seed:PaymentInstructions"]
        });

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default settings row.");
    }

    private static async Task EnsureAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("AdminSeed credentials not configured; skipping admin user seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, AdminRole))
            {
                await userManager.AddToRoleAsync(existing, AdminRole);
            }

            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Admin",
            Role = UserRole.Admin
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AdminRole);
        logger.LogInformation("Seeded admin user {Email}.", email);
    }
}

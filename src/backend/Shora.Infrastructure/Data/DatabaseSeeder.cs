using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Infrastructure.Data;

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
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var adminSeedOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await EnsureRolesAsync(roleManager, logger, cancellationToken);
        await EnsureSettingsAsync(context, seedOptions, logger, cancellationToken);
        await EnsureAdminUserAsync(userManager, adminSeedOptions, logger, cancellationToken);
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

            var role = new IdentityRole<Guid>(roleName)
            {
                NormalizedName = roleName.ToUpperInvariant()
            };

            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                logger.LogWarning("Failed to create role {Role}: {Errors}",
                    roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task EnsureSettingsAsync(
        ApplicationDbContext context,
        SeedOptions seedOptions,
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
            SessionPrice = seedOptions.SessionPrice,
            SessionDurationMinutes = seedOptions.SessionDurationMinutes,
            BufferMinutes = seedOptions.BufferMinutes,
            ReceiptUploadWindowMinutes = seedOptions.ReceiptUploadWindowMinutes,
            CancellationRequestAutoDeclineHours = seedOptions.CancellationRequestAutoDeclineHours,
            ReceiptRetentionMonths = seedOptions.ReceiptRetentionMonths,
            ConsultantWhatsAppNumber = seedOptions.ConsultantWhatsAppNumber,
            VodafoneCashNumber = seedOptions.VodafoneCashNumber,
            InstaPayHandle = seedOptions.InstaPayHandle,
            PaymentInstructions = seedOptions.PaymentInstructions
        });

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default settings row.");
    }

    private static async Task EnsureAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        AdminSeedOptions adminSeedOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminSeedOptions.Email) || string.IsNullOrWhiteSpace(adminSeedOptions.Password))
        {
            logger.LogWarning("AdminSeed credentials not configured; skipping admin user seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(adminSeedOptions.Email);
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
            UserName = adminSeedOptions.Email,
            Email = adminSeedOptions.Email,
            EmailConfirmed = true,
            DisplayName = "Admin",
            Role = UserRole.Admin
        };

        var result = await userManager.CreateAsync(admin, adminSeedOptions.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AdminRole);
        logger.LogInformation("Seeded admin user {Email}.", adminSeedOptions.Email);
    }
}

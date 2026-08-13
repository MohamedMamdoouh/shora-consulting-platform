using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Options;
using Shora.Application.Services;
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
        await EnsureAvailabilityWindowsAsync(context, logger, cancellationToken);
        await EnsureAdminUserAsync(userManager, adminSeedOptions, logger, cancellationToken);

        var slotGenerationService = scope.ServiceProvider.GetRequiredService<SlotGenerationService>();
        await slotGenerationService.GenerateHorizonAsync(cancellationToken);
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
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            logger.LogInformation("Seeded role {Role}.", roleName);
        }

        foreach (var roleName in new[] { ClientRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                throw new InvalidOperationException($"Required role '{roleName}' is missing after seeding.");
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

    private static async Task EnsureAvailabilityWindowsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await context.AvailabilityWindows.AnyAsync(cancellationToken))
        {
            return;
        }

        var defaultWindows = new (DayOfWeek Day, TimeSpan Start, TimeSpan End)[]
        {
            (DayOfWeek.Sunday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
            (DayOfWeek.Monday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
            (DayOfWeek.Tuesday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
            (DayOfWeek.Wednesday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
            (DayOfWeek.Thursday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
        };

        foreach (var (day, start, end) in defaultWindows)
        {
            context.AvailabilityWindows.Add(new AvailabilityWindow
            {
                Id = Guid.NewGuid(),
                DayOfWeek = day,
                StartTime = start,
                EndTime = end,
                IsActive = true
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default availability windows.");
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

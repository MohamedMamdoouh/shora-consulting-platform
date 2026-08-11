using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Email;
using Shora.Application.Email.Outbox;
using Shora.Application.Options;
using Shora.Application.Payments;
using Shora.Application.Services;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;

namespace Shora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<RefreshCookieOptions>(configuration.GetSection(RefreshCookieOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<EmailBrandOptions>(configuration.GetSection(EmailBrandOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));
        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<ReceiptUploadOptions>(configuration.GetSection(ReceiptUploadOptions.SectionName));

        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        if (!string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            services.AddScoped<IFileStorage, NotImplementedFileStorage>();
        }

        services.AddSingleton<IMalwareScanner, PassThroughMalwareScanner>();

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<RefreshCookieService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (hostEnvironment?.IsDevelopment() == true)
        {
            services.AddScoped<IEmailSender, DevLoggingEmailSender>();
        }
        else if (emailOptions.IsConfigured)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, NoOpEmailSender>();
        }

        services.AddScoped<AuthEmailService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<BookingService>();
        services.AddScoped<CancellationService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<ReceiptUploadService>();
        services.AddScoped<ReceiptAntiReplayChecker>();
        services.AddScoped<AdminReceiptReviewService>();
        services.AddScoped<AdminRefundService>();
        services.AddScoped<AdminAvailabilityService>();
        services.AddScoped<AdminBookingListService>();
        services.AddScoped<AdminEarningsService>();
        services.AddScoped<AdminBookingCancellationService>();
        services.AddScoped<AdminBlockedDateService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<ReceiptUploadDeadlineCleanupService>();
        services.AddScoped<CancellationRequestAutoDeclineService>();
        services.AddScoped<BookingAutoCompleteService>();
        services.AddScoped<RefreshTokenPurgeService>();
        services.AddScoped<ReceiptBlobReconciliationService>();
        services.AddScoped<AvailabilityTopUpService>();
        services.AddScoped<OpsMonitoringService>();
        services.AddScoped<AdminOpsMonitoringService>();
        services.AddScoped<ReceiptRetentionPurgeService>();
        services.AddScoped<TempBlobCleanupService>();
        services.AddScoped<JobHeartbeatService>();
        services.AddScoped<OutboxDispatcherService>();
        services.AddScoped<IOutboxEmailRenderer, OutboxEmailRenderer>();
        services.AddSingleton<TransactionEmailLinks>();
        services.AddScoped<BookingTransitionHelper>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
        await DatabaseSeeder.SeedAsync(services, cancellationToken);
    }
}

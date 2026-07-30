using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Email;
using Shora.Application.Options;
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
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<RefreshCookieOptions>(configuration.GetSection(RefreshCookieOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<EmailBrandOptions>(configuration.GetSection(EmailBrandOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));
        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.SectionName));

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<RefreshCookieService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IFileStorage, NotImplementedFileStorage>();

        if (hostEnvironment?.IsDevelopment() == true)
        {
            services.AddScoped<IEmailSender, DevLoggingEmailSender>();
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
        services.AddScoped<SettingsService>();
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<ReceiptUploadDeadlineCleanupService>();
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

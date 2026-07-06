using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Services;
using Shora.Infrastructure.Persistence;
using Shora.Infrastructure.Services;

namespace Shora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<IFileStorage, NotImplementedFileStorage>();

        services.AddScoped<AuthService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<BookingService>();
        services.AddScoped<CancellationService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<SettingsService>();

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

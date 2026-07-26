using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;

namespace Shora.Tests.Common;

public static class TestServiceProviderFactory
{
    public static IServiceProvider Create(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeed:Email"] = "admin@test.local",
                ["AdminSeed:Password"] = "TestPass123!",
                ["Seed:ConsultantWhatsAppNumber"] = "+201012345678",
                ["Seed:VodafoneCashNumber"] = "01012345678",
                ["Seed:InstaPayHandle"] = "test@instapay"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        return services.BuildServiceProvider();
    }
}

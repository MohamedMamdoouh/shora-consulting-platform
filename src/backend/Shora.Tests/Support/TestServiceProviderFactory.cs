using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Domain.Entities;
using Shora.Infrastructure.Persistence;

namespace Shora.Tests.Support;

public static class TestServiceProviderFactory
{
    public static IServiceProvider Create(string databaseName)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

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

        return services.BuildServiceProvider();
    }
}

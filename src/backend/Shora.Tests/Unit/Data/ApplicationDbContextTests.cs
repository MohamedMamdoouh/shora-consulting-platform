using Microsoft.EntityFrameworkCore;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;

namespace Shora.Tests.Unit.Data;

public class ApplicationDbContextTests
{
    private const string ModelBuildConnectionString =
        "Server=localhost;Database=ModelBuild;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public void Model_builds_with_expected_entities()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ModelBuildConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Settings)));
        Assert.NotNull(model.FindEntityType(typeof(Booking)));
        Assert.NotNull(model.FindEntityType(typeof(PaymentReceipt)));
        Assert.NotNull(model.FindEntityType(typeof(OutboxMessage)));
        Assert.NotNull(model.FindEntityType(typeof(RefreshToken)));
    }

    [Fact]
    public void Settings_entity_uses_singleton_id()
    {
        Assert.Equal(1, Settings.SingletonId);
    }
}

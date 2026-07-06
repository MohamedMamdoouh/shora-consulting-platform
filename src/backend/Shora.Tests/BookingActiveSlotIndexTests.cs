using Microsoft.EntityFrameworkCore;
using Shora.Domain.Entities;
using Shora.Infrastructure.Persistence;

namespace Shora.Tests;

public class BookingActiveSlotIndexTests
{
    [Fact]
    public void Booking_has_filtered_unique_index_on_active_availability_slot()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var bookingEntity = context.Model.FindEntityType(typeof(Booking));
        Assert.NotNull(bookingEntity);

        var index = bookingEntity.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Booking.AvailabilitySlotId));

        Assert.True(index.IsUnique);
        Assert.NotNull(index.GetFilter());
        Assert.Contains("PendingPayment", index.GetFilter());
        Assert.Contains("Confirmed", index.GetFilter());
    }
}

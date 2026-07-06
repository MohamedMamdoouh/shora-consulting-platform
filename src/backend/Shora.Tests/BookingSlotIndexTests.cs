using Microsoft.EntityFrameworkCore;
using Shora.Domain.Entities;
using Shora.Infrastructure.Persistence;

namespace Shora.Tests;

public class BookingSlotIndexTests
{
    [Fact]
    public void Booking_has_unique_index_on_availability_slot_when_linked()
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
        Assert.Equal("[AvailabilitySlotId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void AvailabilitySlot_has_unique_index_on_booking_id_when_held()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var slotEntity = context.Model.FindEntityType(typeof(AvailabilitySlot));
        Assert.NotNull(slotEntity);

        var index = slotEntity.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AvailabilitySlot.BookingId));

        Assert.True(index.IsUnique);
        Assert.Equal("[BookingId] IS NOT NULL", index.GetFilter());
    }
}

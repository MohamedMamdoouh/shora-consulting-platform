using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;

namespace Shora.Tests.Unit.Bookings;

public class BookingTransitionHelperTests
{
    [Fact]
    public async Task RecordInitialStatus_writes_audit_with_null_from_status()
    {
        await using var context = CreateContext();
        var helper = CreateHelper(context);
        var booking = await SeedBookingAsync(context, BookingStatus.PendingPayment);

        var result = helper.RecordInitialStatus(
            booking,
            BookingStatus.PendingPayment,
            AuditActor.Client,
            booking.ClientId,
            "Booking reserved");

        Assert.True(result.IsSuccess);
        await context.SaveChangesAsync();

        var audit = await context.BookingStatusAudits.SingleAsync();
        Assert.Null(audit.FromStatus);
        Assert.Equal(BookingStatus.PendingPayment, audit.ToStatus);
        Assert.Equal(AuditActor.Client, audit.Actor);
        Assert.Equal(booking.ClientId, audit.ActorUserId);
        Assert.Equal("Booking reserved", audit.Reason);
    }

    [Fact]
    public async Task ApplyTransition_updates_status_and_writes_audit()
    {
        await using var context = CreateContext();
        var helper = CreateHelper(context);
        var booking = await SeedBookingAsync(context, BookingStatus.PendingPayment);

        var result = helper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            AuditActor.Client,
            BookingStatus.PendingPayment,
            booking.ClientId,
            "Cancelled by client");

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        await context.SaveChangesAsync();

        var audit = await context.BookingStatusAudits.SingleAsync();
        Assert.Equal(BookingStatus.PendingPayment, audit.FromStatus);
        Assert.Equal(BookingStatus.Cancelled, audit.ToStatus);
        Assert.Equal(AuditActor.Client, audit.Actor);
    }

    [Fact]
    public async Task ApplyTransition_rejects_unexpected_current_status()
    {
        await using var context = CreateContext();
        var helper = CreateHelper(context);
        var booking = await SeedBookingAsync(context, BookingStatus.Confirmed);

        var result = helper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            AuditActor.Client,
            BookingStatus.PendingPayment);

        Assert.True(result.IsFailure);
        Assert.Equal("booking.invalid_status", result.Error!.Code);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Empty(context.BookingStatusAudits);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BookingTransitionHelper CreateHelper(ApplicationDbContext context) =>
        new(context, new FixedDateTimeProvider(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)));

    private static async Task<Booking> SeedBookingAsync(ApplicationDbContext context, BookingStatus status)
    {
        var clientId = Guid.NewGuid();
        context.Users.Add(new ApplicationUser
        {
            Id = clientId,
            UserName = "client@test.local",
            Email = "client@test.local",
            EmailConfirmed = true,
            DisplayName = "Client",
            Role = UserRole.Client
        });

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            SlotStartUtc = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
            SlotEndUtc = new DateTime(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc),
            DeliveryMethod = DeliveryMethod.VoiceCall,
            ContactPhone = "+201012345678",
            Status = status,
            CreatedAt = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return booking;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}

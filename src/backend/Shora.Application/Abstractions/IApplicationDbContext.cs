using Microsoft.EntityFrameworkCore;
using Shora.Domain.Entities;

namespace Shora.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<AvailabilitySlot> AvailabilitySlots { get; }

    DbSet<AvailabilityWindow> AvailabilityWindows { get; }

    DbSet<BlockedDate> BlockedDates { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<BookingStatusAudit> BookingStatusAudits { get; }

    DbSet<CancellationRequest> CancellationRequests { get; }

    DbSet<Payment> Payments { get; }

    DbSet<PaymentReceipt> PaymentReceipts { get; }

    DbSet<Settings> Settings { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

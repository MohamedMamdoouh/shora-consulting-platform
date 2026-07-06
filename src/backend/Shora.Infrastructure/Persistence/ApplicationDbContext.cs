using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;
using Shora.Infrastructure.Persistence.Configurations;

namespace Shora.Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    public DbSet<AvailabilityWindow> AvailabilityWindows => Set<AvailabilityWindow>();

    public DbSet<BlockedDate> BlockedDates => Set<BlockedDate>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingStatusAudit> BookingStatusAudits => Set<BookingStatusAudit>();

    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();

    public DbSet<Settings> Settings => Set<Settings>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new AvailabilitySlotConfiguration());
        builder.ApplyConfiguration(new AvailabilityWindowConfiguration());
        builder.ApplyConfiguration(new BlockedDateConfiguration());
        builder.ApplyConfiguration(new BookingConfiguration());
        builder.ApplyConfiguration(new BookingStatusAuditConfiguration());
        builder.ApplyConfiguration(new CancellationRequestConfiguration());
        builder.ApplyConfiguration(new PaymentConfiguration());
        builder.ApplyConfiguration(new PaymentReceiptConfiguration());
        builder.ApplyConfiguration(new SettingsConfiguration());
        builder.ApplyConfiguration(new OutboxMessageConfiguration());
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}

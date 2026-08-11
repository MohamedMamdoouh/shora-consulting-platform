using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;

namespace Shora.Infrastructure.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext, IDataProtectionKeyContext
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

    public DbSet<JobRunHistory> JobRunHistories => Set<JobRunHistory>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTableNames(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    private static void ConfigureIdentityTableNames(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
    }
}

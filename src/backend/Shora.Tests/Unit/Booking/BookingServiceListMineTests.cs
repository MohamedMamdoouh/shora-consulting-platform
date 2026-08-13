using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Tests.Unit.Bookings;

public class BookingServiceListMineTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListMine_returns_only_current_client_bookings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();

        SeedClient(context, clientId);
        SeedClient(context, otherClientId);

        SeedBooking(context, clientId, BookingStatus.Confirmed, FixedNow.AddDays(2));
        SeedBooking(context, otherClientId, BookingStatus.Confirmed, FixedNow.AddDays(3));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Upcoming),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(clientId, await context.Bookings
            .Where(booking => booking.Id == result.Value.Items[0].BookingId)
            .Select(booking => booking.ClientId)
            .SingleAsync(cancellationToken));
    }

    [Fact]
    public async Task ListMine_upcoming_filter_includes_confirmed_and_cancellation_requested_future_bookings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var confirmedId = SeedBooking(context, clientId, BookingStatus.Confirmed, FixedNow.AddDays(1)).Id;
        var cancellationRequestedId = SeedBooking(
            context,
            clientId,
            BookingStatus.CancellationRequested,
            FixedNow.AddDays(2)).Id;
        SeedBooking(context, clientId, BookingStatus.PendingPayment, FixedNow.AddDays(3));
        SeedBooking(context, clientId, BookingStatus.Completed, FixedNow.AddDays(-1));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Upcoming),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(
            [confirmedId, cancellationRequestedId],
            result.Value.Items.Select(item => item.BookingId).ToArray());
    }

    [Fact]
    public async Task ListMine_upcoming_filter_excludes_bookings_with_past_slot_start()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        SeedBooking(context, clientId, BookingStatus.Confirmed, FixedNow.AddHours(-1));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Upcoming),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task ListMine_pending_filter_returns_pending_payment_and_pending_approval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var pendingPaymentId = SeedBooking(
            context,
            clientId,
            BookingStatus.PendingPayment,
            FixedNow.AddDays(1)).Id;
        var pendingApprovalId = SeedBooking(
            context,
            clientId,
            BookingStatus.PendingApproval,
            FixedNow.AddDays(2)).Id;
        SeedBooking(context, clientId, BookingStatus.Confirmed, FixedNow.AddDays(3));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Pending),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(
            [pendingPaymentId, pendingApprovalId],
            result.Value.Items.Select(item => item.BookingId).ToArray());
    }

    [Fact]
    public async Task ListMine_past_filter_orders_most_recent_first_and_paginates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var olderPastId = SeedBooking(context, clientId, BookingStatus.Completed, FixedNow.AddDays(-3)).Id;
        var newerPastId = SeedBooking(context, clientId, BookingStatus.Cancelled, FixedNow.AddDays(-1)).Id;
        var middlePastId = SeedBooking(context, clientId, BookingStatus.Completed, FixedNow.AddDays(-2)).Id;

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var firstPage = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past, Page: 1, PageSize: 2),
            cancellationToken);
        var secondPage = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past, Page: 2, PageSize: 2),
            cancellationToken);

        Assert.True(firstPage.IsSuccess);
        Assert.Equal(3, firstPage.Value!.TotalCount);
        Assert.Equal(2, firstPage.Value.Items.Count);
        Assert.Equal([newerPastId, middlePastId], firstPage.Value.Items.Select(item => item.BookingId).ToArray());

        Assert.True(secondPage.IsSuccess);
        Assert.Single(secondPage.Value!.Items);
        Assert.Equal(olderPastId, secondPage.Value.Items[0].BookingId);
    }

    [Fact]
    public async Task ListMine_upcoming_and_pending_are_unpaginated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        SeedBooking(context, clientId, BookingStatus.PendingPayment, FixedNow.AddDays(1));
        SeedBooking(context, clientId, BookingStatus.PendingApproval, FixedNow.AddDays(2));
        SeedBooking(context, clientId, BookingStatus.PendingPayment, FixedNow.AddDays(3));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Pending, Page: 3, PageSize: 1),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Items.Count);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(3, result.Value.PageSize);
        Assert.Equal(3, result.Value.TotalCount);
    }

    [Fact]
    public async Task ListMine_rejects_invalid_page()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.ListMineAsync(
            Guid.NewGuid(),
            new MyBookingsQuery(MyBookingsStatusFilter.Past, Page: 0),
            cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task ListMine_rejects_page_size_over_max()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.ListMineAsync(
            Guid.NewGuid(),
            new MyBookingsQuery(
                MyBookingsStatusFilter.Past,
                PageSize: MyBookingsQueryLimits.MaxPageSize + 1),
            cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task ListMine_maps_core_fields_without_enrichment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var slotStartUtc = FixedNow.AddDays(1);
        var slotEndUtc = slotStartUtc.AddHours(1);
        var booking = SeedBooking(
            context,
            clientId,
            BookingStatus.Confirmed,
            slotStartUtc,
            slotEndUtc,
            Domain.Enums.DeliveryMethod.VoiceCall,
            "+201012345678");

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Upcoming),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(booking.Id, item.BookingId);
        Assert.Equal(slotStartUtc, item.SlotStartUtc);
        Assert.Equal(slotEndUtc, item.SlotEndUtc);
        Assert.Equal(ContractDeliveryMethod.VoiceCall, item.DeliveryMethod);
        Assert.Equal("+201012345678", item.ContactPhone);
        Assert.Equal(nameof(BookingStatus.Confirmed), item.Status);
        Assert.Null(item.CancellationReasonLabel);
        Assert.Null(item.RefundLabel);
        Assert.Null(item.CancellationRequest);
        Assert.Null(item.PaymentSummary);
        Assert.Null(item.ReceiptThumbnailUrl);
        Assert.Null(item.ConsultantWhatsAppNumber);
    }

    [Fact]
    public async Task ListMine_enriches_cancelled_booking_with_reason_and_refund_due_labels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var booking = SeedBooking(context, clientId, BookingStatus.Cancelled, FixedNow.AddDays(-1));
        SeedPayment(context, booking, PaymentStatus.Approved);
        SeedCancelAudit(context, booking.Id, AuditActor.Client, FixedNow.AddDays(-1));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(MyBookingLabelMapper.CancelledByYou, item.CancellationReasonLabel);
        Assert.Null(item.CancellationDetail);
        Assert.Equal(MyBookingLabelMapper.RefundBeingProcessed, item.RefundLabel);
    }

    [Fact]
    public async Task ListMine_enriches_cancelled_booking_with_refunded_label()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var booking = SeedBooking(context, clientId, BookingStatus.Cancelled, FixedNow.AddDays(-1));
        SeedPayment(context, booking, PaymentStatus.Refunded);
        SeedCancelAudit(context, booking.Id, AuditActor.Admin, FixedNow.AddDays(-1));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(MyBookingLabelMapper.CancelledByInstructor, item.CancellationReasonLabel);
        Assert.Null(item.CancellationDetail);
        Assert.Equal(MyBookingLabelMapper.Refunded, item.RefundLabel);
    }

    [Fact]
    public async Task ListMine_attributes_approved_cancellation_request_to_the_client_with_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var booking = SeedBooking(context, clientId, BookingStatus.Cancelled, FixedNow.AddDays(-1));
        SeedCancelAudit(context, booking.Id, AuditActor.Admin, FixedNow.AddDays(-1));
        context.CancellationRequests.Add(new CancellationRequest
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            RequestedByClientId = clientId,
            RequestedAtUtc = FixedNow.AddDays(-2),
            ClientReason = "  Schedule conflict  ",
            AutoDeclineAtUtc = FixedNow.AddDays(-1).AddHours(-1),
            Status = Domain.Enums.CancellationRequestStatus.Approved,
            ReopenCount = 0
        });

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(MyBookingLabelMapper.CancelledByYou, item.CancellationReasonLabel);
        Assert.Equal("Schedule conflict", item.CancellationDetail);
    }

    [Fact]
    public async Task ListMine_enriches_system_cancelled_booking_with_system_label_and_receipt_detail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var booking = SeedBooking(context, clientId, BookingStatus.Cancelled, FixedNow.AddDays(-1));
        SeedCancelAudit(context, booking.Id, AuditActor.System, FixedNow.AddDays(-1));

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Past),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(MyBookingLabelMapper.CancelledBySystem, item.CancellationReasonLabel);
        Assert.Equal(MyBookingLabelMapper.ReceiptNotUploadedInTime, item.CancellationDetail);
    }

    [Fact]
    public async Task ListMine_enriches_upcoming_booking_with_whatsapp_and_cancellation_request_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);
        SeedSettings(context);

        var booking = SeedBooking(context, clientId, BookingStatus.CancellationRequested, FixedNow.AddDays(1));
        context.CancellationRequests.Add(new CancellationRequest
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            RequestedByClientId = clientId,
            RequestedAtUtc = FixedNow,
            AutoDeclineAtUtc = FixedNow.AddDays(1).AddHours(-1),
            Status = Domain.Enums.CancellationRequestStatus.Pending,
            ReopenCount = 0
        });

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Upcoming),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("+201000000000", item.ConsultantWhatsAppNumber);
        Assert.NotNull(item.CancellationRequest);
        Assert.Equal(ContractCancellationRequestStatus.Pending, item.CancellationRequest!.Status);
        Assert.Equal(0, item.CancellationRequest.ReopenCount);
    }

    [Fact]
    public async Task ListMine_enriches_pending_payment_with_payment_summary_and_decline_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);
        SeedSettings(context);

        var booking = SeedBooking(context, clientId, BookingStatus.PendingPayment, FixedNow.AddDays(1));
        booking.ReceiptUploadDeadlineUtc = FixedNow.AddHours(2);
        var payment = SeedPayment(context, booking, PaymentStatus.AwaitingReceipt);
        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            BlobPath = "receipts/declined.jpg",
            OriginalFileName = "declined.jpg",
            ContentType = "image/jpeg",
            ContentHashSha256 = "hash",
            SizeBytes = 100,
            UploadedAtUtc = FixedNow.AddHours(-1),
            BlobState = BlobState.Finalized,
            MalwareScanStatus = MalwareScanStatus.Clean,
            ReviewStatus = ReceiptReviewStatus.Declined,
            DeclineReason = "Amount mismatch"
        });

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Pending),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.NotNull(item.PaymentSummary);
        Assert.Equal(500m, item.PaymentSummary!.Amount);
        Assert.Equal("01000000000", item.PaymentSummary.VodafoneCashNumber);
        Assert.Equal(FixedNow.AddHours(2), item.PaymentSummary.ReceiptUploadDeadlineUtc);
        Assert.Equal("Amount mismatch", item.PaymentSummary.LatestReceiptDeclineReason);
    }

    [Fact]
    public async Task ListMine_enriches_pending_approval_with_receipt_thumbnail_url()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        await using var context = CreateContext();
        var clientId = Guid.NewGuid();
        SeedClient(context, clientId);

        var booking = SeedBooking(context, clientId, BookingStatus.PendingApproval, FixedNow.AddDays(1));
        var payment = SeedPayment(context, booking, PaymentStatus.UnderReview);
        const string blobPath = "receipts/pending.jpg";
        fileStorage.AddBlob(blobPath, [1, 2, 3], FixedNow);
        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            BlobPath = blobPath,
            OriginalFileName = "pending.jpg",
            ContentType = "image/jpeg",
            ContentHashSha256 = "hash",
            SizeBytes = 100,
            UploadedAtUtc = FixedNow,
            BlobState = BlobState.Finalized,
            MalwareScanStatus = MalwareScanStatus.Clean,
            ReviewStatus = ReceiptReviewStatus.Pending
        });

        await context.SaveChangesAsync(cancellationToken);

        var service = CreateService(context, fileStorage);
        var result = await service.ListMineAsync(
            clientId,
            new MyBookingsQuery(MyBookingsStatusFilter.Pending),
            cancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal($"memory://{blobPath}", item.ReceiptThumbnailUrl);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BookingService CreateService(
        ApplicationDbContext context,
        InMemoryFileStorage? fileStorage = null) =>
        new(
            context,
            new FixedDateTimeProvider(FixedNow),
            new BookingTransitionHelper(context, new FixedDateTimeProvider(FixedNow)),
            new NoOpCacheInvalidator(),
            fileStorage ?? new InMemoryFileStorage(),
            Options.Create(new BookingOptions()),
            Options.Create(new StorageOptions()));

    private static void SeedSettings(ApplicationDbContext context)
    {
        context.Settings.Add(new Settings
        {
            Id = Settings.SingletonId,
            SessionPrice = 500,
            SessionDurationMinutes = 60,
            ConsultantWhatsAppNumber = "+201000000000",
            VodafoneCashNumber = "01000000000",
            InstaPayHandle = "shora@test"
        });
    }

    private static Payment SeedPayment(
        ApplicationDbContext context,
        Booking booking,
        PaymentStatus status)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Status = status,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow
        };

        context.Payments.Add(payment);
        booking.Payment = payment;
        return payment;
    }

    private static void SeedCancelAudit(
        ApplicationDbContext context,
        Guid bookingId,
        AuditActor actor,
        DateTime atUtc)
    {
        context.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            FromStatus = BookingStatus.Confirmed,
            ToStatus = BookingStatus.Cancelled,
            Actor = actor,
            AtUtc = atUtc
        });
    }

    private static void SeedClient(ApplicationDbContext context, Guid clientId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = clientId,
            UserName = $"{clientId}@test.local",
            Email = $"{clientId}@test.local",
            EmailConfirmed = true,
            DisplayName = "Client",
            Role = UserRole.Client
        });
    }

    private static Booking SeedBooking(
        ApplicationDbContext context,
        Guid clientId,
        BookingStatus status,
        DateTime slotStartUtc,
        DateTime? slotEndUtc = null,
        Domain.Enums.DeliveryMethod deliveryMethod = Domain.Enums.DeliveryMethod.Chat,
        string? contactPhone = null)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotEndUtc ?? slotStartUtc.AddHours(1),
            DeliveryMethod = deliveryMethod,
            ContactPhone = contactPhone,
            Status = status,
            CreatedAt = FixedNow
        };

        context.Bookings.Add(booking);
        return booking;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

using Microsoft.Extensions.Options;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Email;
using Shora.Application.Email.Outbox;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Tests.Unit.Email;

public class TransactionEmailTemplatesTests
{
    [Theory]
    [InlineData(OutboxMessageTypes.ClientBookingConfirmedEmail, "تم تأكيد حجزك")]
    [InlineData(OutboxMessageTypes.AdminNewBookingEmail, "حجز جديد مؤكد")]
    [InlineData(OutboxMessageTypes.AdminReceiptUploadedEmail, "إيصال جديد بانتظار المراجعة")]
    [InlineData(OutboxMessageTypes.ClientReceiptDeclinedEmail, "يرجى إعادة رفع الإيصال")]
    [InlineData(OutboxMessageTypes.ClientBookingCancelledEmail, "تم إلغاء الحجز")]
    [InlineData(OutboxMessageTypes.AdminNewCancellationRequestEmail, "طلب إلغاء جديد")]
    [InlineData(OutboxMessageTypes.ClientCancellationRequestDeclinedEmail, "تم رفض طلب الإلغاء")]
    [InlineData(OutboxMessageTypes.ClientRefundConfirmationEmail, "تأكيد الاسترداد")]
    public void Render_transaction_templates_without_unreplaced_tokens(
        string messageType,
        string expectedHeading)
    {
        var context = CreateContext(messageType);
        var links = CreateLinks();
        var service = CreateTemplateService();

        var request = TransactionEmailTemplates.BuildRequest(context, links, "Shora");
        var html = service.Render(request);

        Assert.Contains(expectedHeading, html);
        Assert.Contains(context.Recipient.DisplayName, html);
        Assert.Contains("lang=\"ar\"", html);
        Assert.DoesNotContain("{{", html);
    }

    [Theory]
    [InlineData(OutboxMessageTypes.ClientBookingConfirmedEmail)]
    [InlineData(OutboxMessageTypes.AdminNewBookingEmail)]
    [InlineData(OutboxMessageTypes.AdminReceiptUploadedEmail)]
    [InlineData(OutboxMessageTypes.ClientReceiptDeclinedEmail)]
    [InlineData(OutboxMessageTypes.ClientBookingCancelledEmail)]
    [InlineData(OutboxMessageTypes.AdminNewCancellationRequestEmail)]
    [InlineData(OutboxMessageTypes.ClientCancellationRequestDeclinedEmail)]
    [InlineData(OutboxMessageTypes.ClientRefundConfirmationEmail)]
    public void GetSubject_returns_branded_subject_for_each_message_type(string messageType)
    {
        var subject = TransactionEmailTemplates.GetSubject(messageType, "Shora");

        Assert.Contains("Shora", subject);
        Assert.False(string.IsNullOrWhiteSpace(subject));
    }

    [Fact]
    public void Cancelled_email_includes_who_cancelled_and_system_detail()
    {
        var context = CreateContext(OutboxMessageTypes.ClientBookingCancelledEmail);
        var html = CreateTemplateService().Render(
            TransactionEmailTemplates.BuildRequest(context, CreateLinks(), "Shora"));

        Assert.Contains("من قام بالإلغاء", html);
        Assert.Contains("النظام (تلقائيًا)", html);
        Assert.Contains("لم يتم رفع الإيصال في الوقت المحدد", html);
        Assert.DoesNotContain("{{", html);
    }

    private static TransactionEmailContext CreateContext(string messageType)
    {
        var clientId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var client = new ApplicationUser
        {
            Id = clientId,
            Email = "client@test.local",
            DisplayName = "Alex"
        };

        var booking = new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            Client = client,
            SlotStartUtc = now.AddDays(2),
            SlotEndUtc = now.AddDays(2).AddHours(1),
            DeliveryMethod = DeliveryMethod.Chat,
            ContactPhone = "+201012345678",
            Status = BookingStatus.Confirmed,
            Payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                Amount = 500m,
                Currency = "EGP",
                Status = PaymentStatus.Approved
            }
        };

        var settings = new Settings
        {
            Id = Settings.SingletonId,
            ConsultantWhatsAppNumber = "+201098765432",
            SessionPrice = 500m,
            SessionDurationMinutes = 60,
            BufferMinutes = 15,
            VodafoneCashNumber = "01012345678",
            InstaPayHandle = "test@instapay",
            ReceiptUploadWindowMinutes = 60,
            CancellationRequestAutoDeclineHours = 24,
            ReceiptRetentionMonths = 24
        };

        return new TransactionEmailContext
        {
            MessageType = messageType,
            Recipient = messageType is OutboxMessageTypes.AdminNewBookingEmail
                or OutboxMessageTypes.AdminReceiptUploadedEmail
                or OutboxMessageTypes.AdminNewCancellationRequestEmail
                ? new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = "admin@test.local",
                    DisplayName = "Admin",
                    Role = UserRole.Admin
                }
                : client,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment,
            ReasonCode = "Other",
            ReasonNote = "ملاحظة تجريبية",
            ReceiptUploadDeadlineUtc = now.AddHours(6),
            RefundReference = "REF-123",
            RefundNote = "تم التحويل",
            RefundAmount = 500m,
            RefundCurrency = "EGP",
            AutoDeclineAtUtc = now.AddHours(12),
            ClientReason = "لا أستطيع الحضور",
            CancellationReasonLabel = MyBookingLabelMapper.CancelledBySystem,
            CancellationDetail = MyBookingLabelMapper.ReceiptNotUploadedInTime
        };
    }

    private static TransactionEmailLinks CreateLinks() =>
        new(Options.Create(new FrontendOptions { BaseUrl = "https://app.test" }));

    private static EmailTemplateService CreateTemplateService() =>
        new(Options.Create(new EmailBrandOptions { BrandName = "Shora" }));
}

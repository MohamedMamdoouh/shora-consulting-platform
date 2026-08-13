namespace Shora.Application.Common;

public static class OutboxMessageTypes
{
    public const string ClientBookingCancelledEmail = "ClientBookingCancelledEmail";

    public const string AdminNewCancellationRequestEmail = "AdminNewCancellationRequestEmail";

    public const string AdminReceiptUploadedEmail = "AdminReceiptUploadedEmail";

    public const string ClientBookingConfirmedEmail = "ClientBookingConfirmedEmail";

    public const string AdminNewBookingEmail = "AdminNewBookingEmail";

    public const string ClientReceiptDeclinedEmail = "ClientReceiptDeclinedEmail";

    public const string ClientRefundConfirmationEmail = "ClientRefundConfirmationEmail";

    public const string ClientCancellationRequestDeclinedEmail = "ClientCancellationRequestDeclinedEmail";
}

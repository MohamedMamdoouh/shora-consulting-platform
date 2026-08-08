namespace Shora.Api.Infrastructure;

public static class RateLimitPolicies
{
    public const string AuthCredential = nameof(AuthCredential);

    public const string AuthRecovery = nameof(AuthRecovery);

    public const string AuthRefresh = nameof(AuthRefresh);

    public const string PublicAvailability = nameof(PublicAvailability);

    public const string BookingReserve = nameof(BookingReserve);

    public const string ReceiptUpload = nameof(ReceiptUpload);

    public const string CancellationRequest = nameof(CancellationRequest);
}

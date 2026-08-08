namespace Shora.Application.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthCredentialPermitLimit { get; set; } = 5;

    public int AuthCredentialWindowMinutes { get; set; } = 1;

    public int AuthRecoveryPermitLimit { get; set; } = 5;

    public int AuthRecoveryWindowMinutes { get; set; } = 1;

    public int AuthRefreshPermitLimit { get; set; } = 10;

    public int AuthRefreshWindowMinutes { get; set; } = 1;

    public int PublicAvailabilityPermitLimit { get; set; } = 30;

    public int PublicAvailabilityWindowMinutes { get; set; } = 1;

    public int BookingReservePermitLimit { get; set; } = 10;

    public int BookingReserveWindowMinutes { get; set; } = 1;

    public int CancellationRequestPermitLimit { get; set; } = 5;

    public int CancellationRequestWindowMinutes { get; set; } = 1;
}

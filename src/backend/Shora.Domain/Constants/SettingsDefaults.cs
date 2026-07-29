namespace Shora.Domain.Constants;

public static class SettingsDefaults
{
    public const decimal SessionPrice = 500m;

    public const int SessionDurationMinutes = 60;

    public const int BufferMinutes = 15;

    public const int ReceiptUploadWindowMinutes = 60;

    public const int CancellationRequestAutoDeclineHours = 1;

    public const int ReceiptRetentionMonths = 24;

    public const int UnconfirmedHoldCap = 3;
}

namespace Shora.Contracts.Settings;

public sealed record AdminSettingsResponse(
    decimal SessionPrice,
    int SessionDurationMinutes,
    int BufferMinutes,
    int ReceiptUploadWindowMinutes,
    int CancellationRequestAutoDeclineHours,
    string ConsultantWhatsAppNumber,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions,
    int ReceiptRetentionMonths);

public sealed record UpdateAdminSettingsRequest(
    decimal SessionPrice,
    int SessionDurationMinutes,
    int BufferMinutes,
    int ReceiptUploadWindowMinutes,
    int CancellationRequestAutoDeclineHours,
    string ConsultantWhatsAppNumber,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions);

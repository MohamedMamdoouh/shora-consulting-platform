using Shora.Domain.Constants;

namespace Shora.Application.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public decimal SessionPrice { get; set; } = SettingsDefaults.SessionPrice;

    public int SessionDurationMinutes { get; set; } = SettingsDefaults.SessionDurationMinutes;

    public int BufferMinutes { get; set; } = SettingsDefaults.BufferMinutes;

    public int ReceiptUploadWindowMinutes { get; set; } = SettingsDefaults.ReceiptUploadWindowMinutes;

    public int CancellationRequestAutoDeclineHours { get; set; } = SettingsDefaults.CancellationRequestAutoDeclineHours;

    public int ReceiptRetentionMonths { get; set; } = SettingsDefaults.ReceiptRetentionMonths;

    public string ConsultantWhatsAppNumber { get; set; } = "+201000000000";

    public string VodafoneCashNumber { get; set; } = "01000000000";

    public string InstaPayHandle { get; set; } = "test@instapay";

    public string? PaymentInstructions { get; set; }
}

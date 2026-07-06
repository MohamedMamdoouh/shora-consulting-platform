namespace Shora.Domain.Entities;

public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public decimal SessionPrice { get; set; } = 500m;

    public int SessionDurationMinutes { get; set; } = 60;

    public int BufferMinutes { get; set; } = 15;

    public string ConsultantWhatsAppNumber { get; set; } = string.Empty;

    public string VodafoneCashNumber { get; set; } = string.Empty;

    public string InstaPayHandle { get; set; } = string.Empty;

    public string? PaymentInstructions { get; set; }

    public int ReceiptUploadWindowMinutes { get; set; } = 60;

    public int CancellationRequestAutoDeclineHours { get; set; } = 1;

    public int ReceiptRetentionMonths { get; set; } = 24;
}

namespace Shora.Domain.Entities;

public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public decimal SessionPrice { get; set; }

    public int SessionDurationMinutes { get; set; }

    public int BufferMinutes { get; set; }

    public string ConsultantWhatsAppNumber { get; set; } = string.Empty;

    public string VodafoneCashNumber { get; set; } = string.Empty;

    public string InstaPayHandle { get; set; } = string.Empty;

    public string? PaymentInstructions { get; set; }

    public int ReceiptUploadWindowMinutes { get; set; }

    public int CancellationRequestAutoDeclineHours { get; set; }

    public int ReceiptRetentionMonths { get; set; }

}
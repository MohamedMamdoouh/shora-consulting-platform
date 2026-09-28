using Shora.Application.AdminSettings;
using Shora.Contracts.Settings;

namespace Shora.Tests.Unit.AdminSettings;

public class SettingsUpdateValidatorTests
{
    private static UpdateAdminSettingsRequest ValidRequest() =>
        new(
            SessionPrice: 500m,
            SessionDurationMinutes: 60,
            BufferMinutes: 15,
            ReceiptUploadWindowMinutes: 60,
            CancellationRequestAutoDeclineHours: 1,
            ConsultantWhatsAppNumber: "+201012345678",
            VodafoneCashNumber: "01012345678",
            InstaPayHandle: "consultant@instapay",
            PaymentInstructions: "Include your name in the transfer note.");

    [Fact]
    public void Validate_accepts_valid_request()
    {
        var result = SettingsUpdateValidator.Validate(ValidRequest());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.Equal("+201012345678", result.Value!.ConsultantWhatsAppNumber);
        Assert.Equal("01012345678", result.Value.VodafoneCashNumber);
        Assert.Equal("consultant@instapay", result.Value.InstaPayHandle);
    }

    [Fact]
    public void Validate_rejects_non_positive_session_price()
    {
        var request = ValidRequest() with { SessionPrice = 0m };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("sessionPrice", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_session_price_with_more_than_two_decimals()
    {
        var request = ValidRequest() with { SessionPrice = 500.123m };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("sessionPrice", result.Errors.Keys);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(241)]
    public void Validate_rejects_session_duration_outside_range(int durationMinutes)
    {
        var request = ValidRequest() with { SessionDurationMinutes = durationMinutes };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("sessionDurationMinutes", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_negative_buffer_minutes()
    {
        var request = ValidRequest() with { BufferMinutes = -1 };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("bufferMinutes", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_receipt_upload_window_below_minimum()
    {
        var request = ValidRequest() with { ReceiptUploadWindowMinutes = 4 };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("receiptUploadWindowMinutes", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_negative_auto_decline_hours()
    {
        var request = ValidRequest() with { CancellationRequestAutoDeclineHours = -1 };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("cancellationRequestAutoDeclineHours", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_invalid_consultant_whats_app_number()
    {
        var request = ValidRequest() with { ConsultantWhatsAppNumber = "not-a-phone" };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("consultantWhatsAppNumber", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_invalid_vodafone_cash_number()
    {
        var request = ValidRequest() with { VodafoneCashNumber = "123" };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("vodafoneCashNumber", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_empty_insta_pay_handle()
    {
        var request = ValidRequest() with { InstaPayHandle = "   " };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("instaPayHandle", result.Errors.Keys);
    }

    [Fact]
    public void Validate_rejects_payment_instructions_over_max_length()
    {
        var request = ValidRequest() with { PaymentInstructions = new string('x', 2001) };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("paymentInstructions", result.Errors.Keys);
    }

    [Fact]
    public void Validate_collects_multiple_field_errors()
    {
        var request = ValidRequest() with
        {
            SessionPrice = -1m,
            SessionDurationMinutes = 10,
            InstaPayHandle = ""
        };

        var result = SettingsUpdateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }
}

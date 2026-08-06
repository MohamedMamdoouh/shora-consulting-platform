using Shora.Application.Bookings;
using Shora.Contracts.Settings;

namespace Shora.Application.AdminSettings;

public sealed record ValidatedSettingsUpdate(
    decimal SessionPrice,
    int SessionDurationMinutes,
    int BufferMinutes,
    int ReceiptUploadWindowMinutes,
    int CancellationRequestAutoDeclineHours,
    string ConsultantWhatsAppNumber,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions);

public sealed class SettingsUpdateValidationResult
{
    private SettingsUpdateValidationResult(
        Dictionary<string, string[]> errors,
        ValidatedSettingsUpdate? value)
    {
        Errors = errors;
        Value = value;
    }

    public Dictionary<string, string[]> Errors { get; }

    public ValidatedSettingsUpdate? Value { get; }

    public bool IsValid => Errors.Count == 0 && Value is not null;

    public static SettingsUpdateValidationResult Success(ValidatedSettingsUpdate value) =>
        new([], value);

    public static SettingsUpdateValidationResult Failure(Dictionary<string, string[]> errors) =>
        new(errors, null);
}

public static class SettingsUpdateValidator
{
    private const int MinSessionDurationMinutes = 30;
    private const int MaxSessionDurationMinutes = 240;
    private const int MinReceiptUploadWindowMinutes = 5;
    private const int MaxPaymentInstructionsLength = 2000;
    private const int MaxInstaPayHandleLength = 100;

    public static SettingsUpdateValidationResult Validate(UpdateAdminSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        ValidateSessionPrice(request.SessionPrice, errors);
        ValidateRange(
            request.SessionDurationMinutes,
            MinSessionDurationMinutes,
            MaxSessionDurationMinutes,
            nameof(UpdateAdminSettingsRequest.SessionDurationMinutes),
            errors,
            $"Session duration must be between {MinSessionDurationMinutes} and {MaxSessionDurationMinutes} minutes.");
        ValidateMinimum(
            request.BufferMinutes,
            0,
            nameof(UpdateAdminSettingsRequest.BufferMinutes),
            errors,
            "Buffer minutes must be zero or greater.");
        ValidateMinimum(
            request.ReceiptUploadWindowMinutes,
            MinReceiptUploadWindowMinutes,
            nameof(UpdateAdminSettingsRequest.ReceiptUploadWindowMinutes),
            errors,
            $"Receipt upload window must be at least {MinReceiptUploadWindowMinutes} minutes.");
        ValidateMinimum(
            request.CancellationRequestAutoDeclineHours,
            0,
            nameof(UpdateAdminSettingsRequest.CancellationRequestAutoDeclineHours),
            errors,
            "Cancellation auto-decline hours must be zero or greater.");

        var whatsApp = ValidateE164Phone(
            request.ConsultantWhatsAppNumber,
            nameof(UpdateAdminSettingsRequest.ConsultantWhatsAppNumber),
            errors,
            "Consultant WhatsApp number must be a valid E.164 phone number.");

        var vodafoneCash = ValidateEgyptianMobile(
            request.VodafoneCashNumber,
            nameof(UpdateAdminSettingsRequest.VodafoneCashNumber),
            errors,
            "Vodafone Cash number must be a valid Egyptian mobile number.");

        var instaPayHandle = ValidateInstaPayHandle(request.InstaPayHandle, errors);
        var paymentInstructions = ValidatePaymentInstructions(request.PaymentInstructions, errors);

        if (errors.Count > 0)
        {
            return SettingsUpdateValidationResult.Failure(errors);
        }

        return SettingsUpdateValidationResult.Success(new ValidatedSettingsUpdate(
            request.SessionPrice,
            request.SessionDurationMinutes,
            request.BufferMinutes,
            request.ReceiptUploadWindowMinutes,
            request.CancellationRequestAutoDeclineHours,
            whatsApp!,
            vodafoneCash!,
            instaPayHandle!,
            paymentInstructions));
    }

    private static void ValidateSessionPrice(decimal sessionPrice, Dictionary<string, string[]> errors)
    {
        const string field = nameof(UpdateAdminSettingsRequest.SessionPrice);

        if (sessionPrice <= 0)
        {
            AddError(errors, field, "Session price must be greater than zero.");
            return;
        }

        if (decimal.Round(sessionPrice, 2) != sessionPrice)
        {
            AddError(errors, field, "Session price must have at most two decimal places.");
        }
    }

    private static void ValidateRange(
        int value,
        int min,
        int max,
        string field,
        Dictionary<string, string[]> errors,
        string message)
    {
        if (value < min || value > max)
        {
            AddError(errors, field, message);
        }
    }

    private static void ValidateMinimum(
        int value,
        int minimum,
        string field,
        Dictionary<string, string[]> errors,
        string message)
    {
        if (value < minimum)
        {
            AddError(errors, field, message);
        }
    }

    private static string? ValidateE164Phone(
        string? phone,
        string field,
        Dictionary<string, string[]> errors,
        string message)
    {
        var result = PhoneNormalizer.NormalizeToE164(phone);
        if (result.IsFailure)
        {
            AddError(errors, field, message);
            return null;
        }

        return result.Value;
    }

    private static string? ValidateEgyptianMobile(
        string? phone,
        string field,
        Dictionary<string, string[]> errors,
        string message)
    {
        var result = PhoneNormalizer.NormalizeToE164(phone);
        if (result.IsFailure)
        {
            AddError(errors, field, message);
            return null;
        }

        return ToLocalEgyptMobile(result.Value!);
    }

    private static string? ValidateInstaPayHandle(string? handle, Dictionary<string, string[]> errors)
    {
        const string field = nameof(UpdateAdminSettingsRequest.InstaPayHandle);
        var trimmed = handle?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            AddError(errors, field, "InstaPay handle is required.");
            return null;
        }

        if (trimmed.Length > MaxInstaPayHandleLength)
        {
            AddError(errors, field, $"InstaPay handle must be at most {MaxInstaPayHandleLength} characters.");
            return null;
        }

        return trimmed;
    }

    private static string? ValidatePaymentInstructions(string? instructions, Dictionary<string, string[]> errors)
    {
        if (instructions is null)
        {
            return null;
        }

        var trimmed = instructions.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxPaymentInstructionsLength)
        {
            AddError(
                errors,
                nameof(UpdateAdminSettingsRequest.PaymentInstructions),
                $"Payment instructions must be at most {MaxPaymentInstructionsLength} characters.");
            return null;
        }

        return trimmed;
    }

    private static string ToLocalEgyptMobile(string e164)
    {
        if (e164.StartsWith("+20", StringComparison.Ordinal) && e164.Length > 3)
        {
            return "0" + e164[3..];
        }

        return e164;
    }

    private static void AddError(Dictionary<string, string[]> errors, string field, string message)
    {
        var key = char.ToLowerInvariant(field[0]) + field[1..];
        errors[key] = [message];
    }
}

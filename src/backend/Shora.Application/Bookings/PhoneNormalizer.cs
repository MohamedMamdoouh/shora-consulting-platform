using PhoneNumbers;
using Shora.Application.Common;
using Shora.Application.Common.Results;

namespace Shora.Application.Bookings;

public static class PhoneNormalizer
{
    private const string DefaultRegion = "EG";
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    public static Result<string> NormalizeToE164(string? phone, string defaultRegion = DefaultRegion)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Error.Validation(
                ErrorCodes.Booking.ContactPhoneInvalid,
                "Contact phone number is invalid.");
        }

        try
        {
            var parsed = PhoneUtil.Parse(phone.Trim(), defaultRegion);
            if (!PhoneUtil.IsValidNumber(parsed))
            {
                return Error.Validation(
                    ErrorCodes.Booking.ContactPhoneInvalid,
                    "Contact phone number is invalid.");
            }

            return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            return Error.Validation(
                ErrorCodes.Booking.ContactPhoneInvalid,
                "Contact phone number is invalid.");
        }
    }
}

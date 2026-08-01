using System.Text;
using Shora.Application.Common;
using Shora.Application.Common.Results;

namespace Shora.Application.Payments;

public static class ReceiptContentValidator
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    };

    public static Result Validate(byte[] content, string declaredContentType)
    {
        if (content.Length == 0)
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidReceiptFile,
                "Receipt file is empty.");
        }

        if (string.IsNullOrWhiteSpace(declaredContentType))
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidReceiptFile,
                "Receipt content type is required.");
        }

        var normalizedContentType = declaredContentType.Trim().ToLowerInvariant();
        if (!AllowedContentTypes.Contains(normalizedContentType))
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidReceiptFile,
                "Receipt file type is not allowed.");
        }

        if (!MatchesMagicBytes(content, normalizedContentType))
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidReceiptFile,
                "Receipt file content does not match its declared type.");
        }

        return Result.Success();
    }

    private static bool MatchesMagicBytes(byte[] content, string normalizedContentType) =>
        normalizedContentType switch
        {
            "image/jpeg" => content.Length >= 3
                && content[0] == 0xFF
                && content[1] == 0xD8
                && content[2] == 0xFF,
            "image/png" => content.Length >= 8
                && content[0] == 0x89
                && content[1] == 0x50
                && content[2] == 0x4E
                && content[3] == 0x47
                && content[4] == 0x0D
                && content[5] == 0x0A
                && content[6] == 0x1A
                && content[7] == 0x0A,
            "image/webp" => content.Length >= 12
                && Encoding.ASCII.GetString(content, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(content, 8, 4) == "WEBP",
            "application/pdf" => content.Length >= 4
                && Encoding.ASCII.GetString(content, 0, 4) == "%PDF",
            _ => false
        };
}

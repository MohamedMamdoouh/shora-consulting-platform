using Shora.Domain.Enums;

namespace Shora.Application.Payments;

public static class ReceiptReviewWarningMapper
{
    public static IReadOnlyList<string> ToWarningCodes(ReceiptReviewWarning warning) =>
        warning == ReceiptReviewWarning.DuplicateContentHash
            ? [nameof(ReceiptReviewWarning.DuplicateContentHash)]
            : [];
}

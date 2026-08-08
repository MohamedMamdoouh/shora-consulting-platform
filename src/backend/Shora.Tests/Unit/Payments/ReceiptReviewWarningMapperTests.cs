using Shora.Application.Payments;
using Shora.Domain.Enums;

namespace Shora.Tests.Unit.Payments;

public class ReceiptReviewWarningMapperTests
{
    [Fact]
    public void ToWarningCodes_returns_empty_list_when_no_warning_is_present()
    {
        var codes = ReceiptReviewWarningMapper.ToWarningCodes(ReceiptReviewWarning.None);

        Assert.Empty(codes);
    }

    [Fact]
    public void ToWarningCodes_exposes_duplicate_content_hash_warning_for_api_responses()
    {
        var codes = ReceiptReviewWarningMapper.ToWarningCodes(ReceiptReviewWarning.DuplicateContentHash);

        Assert.Equal([nameof(ReceiptReviewWarning.DuplicateContentHash)], codes);
    }
}

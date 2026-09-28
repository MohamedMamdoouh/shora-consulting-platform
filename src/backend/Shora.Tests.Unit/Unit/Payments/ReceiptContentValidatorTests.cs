using Shora.Application.Common;
using Shora.Application.Payments;

namespace Shora.Tests.Unit.Payments;

public class ReceiptContentValidatorTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, " IMAGE/WEBP ")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }, "application/pdf")]
    public void Validate_accepts_allowed_types_with_matching_magic_bytes(byte[] content, string contentType)
    {
        var result = ReceiptContentValidator.Validate(content, contentType);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_spoofed_content_type()
    {
        var content = "not-an-image"u8.ToArray();

        var result = ReceiptContentValidator.Validate(content, "image/png");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Payment.InvalidReceiptFile, result.Error!.Code);
    }

    [Fact]
    public void Validate_rejects_disallowed_content_type()
    {
        var content = "%PDF-1.4"u8.ToArray();

        var result = ReceiptContentValidator.Validate(content, "text/plain");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Payment.InvalidReceiptFile, result.Error!.Code);
    }

    [Fact]
    public void Validate_rejects_empty_file()
    {
        var result = ReceiptContentValidator.Validate([], "image/jpeg");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Payment.InvalidReceiptFile, result.Error!.Code);
    }
}

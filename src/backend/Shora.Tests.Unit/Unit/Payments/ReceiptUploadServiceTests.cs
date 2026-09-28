using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Contracts.Payments;

namespace Shora.Tests.Unit.Payments;

public class ReceiptUploadServiceTests
{
    [Fact]
    public async Task UploadAsync_rejects_declared_size_above_configured_limit_before_touching_dependencies()
    {
        using var content = new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]);
        var service = new ReceiptUploadService(
            dbContext: null!,
            fileStorage: null!,
            malwareScanner: null!,
            dateTimeProvider: null!,
            transitionHelper: null!,
            antiReplayChecker: null!,
            receiptUploadOptions: Options.Create(new ReceiptUploadOptions { MaxSizeBytes = 3 }),
            logger: NullLogger<ReceiptUploadService>.Instance);

        var result = await service.UploadAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            content,
            "image/jpeg",
            "receipt.jpg",
            declaredSizeBytes: 4,
            PaymentMethod.VodafoneCash,
            senderReference: null,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Payment.ReceiptTooLarge, result.Error!.Code);
        Assert.Equal(ErrorKind.PayloadTooLarge, result.Error.Kind);
    }
}

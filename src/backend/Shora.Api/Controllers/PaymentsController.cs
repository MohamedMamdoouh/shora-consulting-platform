using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shora.Api.Infrastructure;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/payments")]
[Authorize(Roles = AppRoles.Client)]
public sealed class PaymentsController(
    ReceiptUploadService receiptUploadService,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("{bookingId:guid}/receipt")]
    [EnableRateLimiting(RateLimitPolicies.ReceiptUpload)]
    [EndpointName("Payments.UploadReceipt")]
    [EndpointSummary("Upload a payment receipt for a pending-payment booking")]
    [ProducesResponseType(typeof(UploadReceiptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadReceipt(
        Guid bookingId,
        [FromForm] IFormFile? image,
        [FromForm] string? method,
        [FromForm] string? senderReference,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        if (image is null || image.Length == 0)
        {
            return ToProblem(Error.Validation(
                ErrorCodes.Payment.InvalidReceiptFile,
                "Receipt image file is required."));
        }

        if (string.IsNullOrWhiteSpace(method) || !Enum.TryParse<PaymentMethod>(method, ignoreCase: true, out var parsedMethod))
        {
            return ToProblem(Error.Validation(
                ErrorCodes.Payment.InvalidMethod,
                "Payment method must be VodafoneCash or InstaPay."));
        }

        await using var stream = image.OpenReadStream();
        var result = await receiptUploadService.UploadAsync(
            clientId,
            bookingId,
            stream,
            image.ContentType,
            image.FileName,
            image.Length,
            parsedMethod,
            senderReference,
            cancellationToken);

        return FromResult(result);
    }
}

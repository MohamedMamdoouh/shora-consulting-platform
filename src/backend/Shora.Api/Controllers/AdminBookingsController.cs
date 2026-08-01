using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/bookings")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminBookingsController(
    AdminReceiptReviewService adminReceiptReviewService,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("{id:guid}/receipts")]
    [EndpointName("AdminBookings.GetReceipts")]
    [EndpointSummary("Get payment receipt attempt history with short-lived read URLs")]
    [ProducesResponseType(typeof(AdminBookingReceiptsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipts(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminReceiptReviewService.GetReceiptsAsync(adminId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/receipts/approve")]
    [EndpointName("AdminBookings.ApproveReceipt")]
    [EndpointSummary("Approve the pending payment receipt and confirm the booking")]
    [ProducesResponseType(typeof(AdminReceiptDecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveReceipt(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminReceiptReviewService.ApproveAsync(adminId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/receipts/decline")]
    [EndpointName("AdminBookings.DeclineReceipt")]
    [EndpointSummary("Decline the pending payment receipt and reopen the upload window")]
    [ProducesResponseType(typeof(AdminReceiptDecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeclineReceipt(
        Guid id,
        [FromBody] DeclineReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminReceiptReviewService.DeclineAsync(adminId, id, request, cancellationToken);
        return FromResult(result);
    }
}

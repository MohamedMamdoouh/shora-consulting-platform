using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Booking;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/bookings")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminBookingsController(
    AdminBookingListService adminBookingListService,
    AdminReceiptReviewService adminReceiptReviewService,
    AdminBookingCancellationService adminBookingCancellationService,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("AdminBookings.List")]
    [EndpointSummary("List bookings for the admin dashboard")]
    [ProducesResponseType(typeof(AdminBookingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] AdminBookingStatusFilter? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = AdminBookingsQueryLimits.DefaultPage,
        [FromQuery] int pageSize = AdminBookingsQueryLimits.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var validation = AdminBookingsQueryValidator.Validate(
            new AdminBookingsQuery(status, from, to, page, pageSize));

        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await adminBookingListService.ListAsync(validation.Value!, cancellationToken);
        return FromResult(result);
    }

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

    [HttpPost("{id:guid}/cancel")]
    [EndpointName("AdminBookings.Cancel")]
    [EndpointSummary("Directly cancel a booking before the session starts")]
    [ProducesResponseType(typeof(AdminBookingCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminBookingCancellationService.CancelAsync(adminId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/cancellation-requests/approve")]
    [EndpointName("AdminBookings.ApproveCancellationRequest")]
    [EndpointSummary("Approve a client cancellation request and cancel the booking")]
    [ProducesResponseType(typeof(AdminBookingCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveCancellationRequest(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminBookingCancellationService.ApproveCancellationRequestAsync(adminId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/cancellation-requests/decline")]
    [EndpointName("AdminBookings.DeclineCancellationRequest")]
    [EndpointSummary("Decline a client cancellation request and keep the booking confirmed")]
    [ProducesResponseType(typeof(AdminBookingCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeclineCancellationRequest(
        Guid id,
        [FromBody] DeclineCancellationRequestBody request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminBookingCancellationService.DeclineCancellationRequestAsync(adminId, id, request, cancellationToken);
        return FromResult(result);
    }
}

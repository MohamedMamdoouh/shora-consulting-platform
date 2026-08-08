using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shora.Api.Infrastructure;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Booking;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/bookings")]
[Authorize(Roles = AppRoles.Client)]
public sealed class BookingsController(
    BookingService bookingService,
    PaymentService paymentService,
    CancellationService cancellationService,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.BookingReserve)]
    [EndpointName("Bookings.Reserve")]
    [EndpointSummary("Reserve an availability slot and create a pending-payment booking")]
    [ProducesResponseType(typeof(ReserveBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Reserve(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await bookingService.ReserveAsync(clientId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("mine")]
    [EndpointName("Bookings.ListMine")]
    [EndpointSummary("List the authenticated client's bookings")]
    [ProducesResponseType(typeof(MyBookingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMine(
        [FromQuery] MyBookingsStatusFilter? status,
        [FromQuery] int page = MyBookingsQueryLimits.DefaultPage,
        [FromQuery] int pageSize = MyBookingsQueryLimits.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await bookingService.ListMineAsync(
            clientId,
            new MyBookingsQuery(status, page, pageSize),
            cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [EndpointName("Bookings.CancelHold")]
    [EndpointSummary("Cancel an unpaid booking hold and release the slot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelHold(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await bookingService.CancelHoldAsync(clientId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/payment-instructions")]
    [EndpointName("Bookings.GetPaymentInstructions")]
    [EndpointSummary("Get manual payment transfer details for a pending-payment booking")]
    [ProducesResponseType(typeof(PaymentInstructionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetPaymentInstructions(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await paymentService.GetPaymentInstructionsAsync(clientId, id, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/cancellation-requests")]
    [EnableRateLimiting(RateLimitPolicies.CancellationRequest)]
    [EndpointName("Bookings.RequestCancellation")]
    [EndpointSummary("Request cancellation of a confirmed booking")]
    [ProducesResponseType(typeof(CancellationRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestCancellation(
        Guid id,
        CancellationRequestBody body,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await cancellationService.RequestCancellationAsync(clientId, id, body, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/cancellation-requests/decision-seen")]
    [EndpointName("Bookings.MarkCancellationDecisionSeen")]
    [EndpointSummary("Acknowledge a declined or auto-declined cancellation decision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkCancellationDecisionSeen(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await cancellationService.MarkDecisionSeenAsync(clientId, id, cancellationToken);
        return FromResult(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/payments")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminPaymentsController(
    AdminRefundService adminRefundService,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("{id:guid}/refunds/record")]
    [EndpointName("AdminPayments.RecordRefund")]
    [EndpointSummary("Record a manual out-of-band refund for a cancelled booking")]
    [ProducesResponseType(typeof(PaymentRefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordRefund(
        Guid id,
        [FromBody] RecordRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminRefundService.RecordRefundAsync(adminId, id, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/refunds/revoke")]
    [EndpointName("AdminPayments.RevokeRefund")]
    [EndpointSummary("Revoke a mistakenly recorded refund and reopen refund-due state")]
    [ProducesResponseType(typeof(PaymentRefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeRefund(
        Guid id,
        [FromBody] RevokeRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return ToProblem(Error.Unauthorized(
                ErrorCodes.Auth.UserNotFound,
                "User is not authenticated."));
        }

        var result = await adminRefundService.RevokeRefundAsync(adminId, id, request, cancellationToken);
        return FromResult(result);
    }
}

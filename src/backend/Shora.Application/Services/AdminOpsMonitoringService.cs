using Shora.Application.Ops;
using Shora.Contracts.Ops;

namespace Shora.Application.Services;

public sealed class AdminOpsMonitoringService(OpsMonitoringService opsMonitoringService)
{
    public async Task<AdminOpsAlertsResponse> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var alerts = await opsMonitoringService.EvaluateAlertsAsync(cancellationToken);

        return new AdminOpsAlertsResponse(alerts
            .Select(alert => new AdminOpsAlertDto(
                alert.Kind.ToString(),
                alert.Severity.ToString(),
                alert.Message,
                alert.RunbookId,
                alert.Context))
            .ToList());
    }

    public static AdminOpsRunbooksResponse GetRunbooks() =>
        new(OpsRunbookCatalog.GetAll());
}

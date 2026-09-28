using System.Reflection;
using Shora.Application.Ops;

namespace Shora.Tests.Unit.Ops;

public class OpsRunbookCatalogTests
{
    [Fact]
    public void GetAll_includes_every_OpsRunbookIds_constant_exactly_once()
    {
        var expectedIds = typeof(OpsRunbookIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(id => id)
            .ToList();

        var actualIds = OpsRunbookCatalog.GetAll()
            .Select(runbook => runbook.Id)
            .OrderBy(id => id)
            .ToList();

        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public void runbooks_json_has_no_duplicate_ids()
    {
        var ids = OpsRunbookCatalog.GetAll().Select(runbook => runbook.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TryGetById_returns_runbook_for_known_id()
    {
        var runbook = OpsRunbookCatalog.TryGetById(OpsRunbookIds.PendingApprovalBacklog);

        Assert.NotNull(runbook);
        Assert.Equal(OpsRunbookIds.PendingApprovalBacklog, runbook!.Id);
        Assert.NotEmpty(runbook.Steps);
    }

    [Fact]
    public void TryGetById_returns_null_for_unknown_id()
    {
        var runbook = OpsRunbookCatalog.TryGetById("unknown-runbook");

        Assert.Null(runbook);
    }
}

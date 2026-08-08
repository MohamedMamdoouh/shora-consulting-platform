using System.Text.Json;
using Shora.Contracts.Ops;

namespace Shora.Application.Ops;

public static class OpsRunbookCatalog
{
    private const string ResourceName = "Shora.Application.Ops.runbooks.json";

    private static readonly Lazy<IReadOnlyList<AdminOpsRunbookDto>> RunbooksLazy = new(LoadRunbooks);

    public static IReadOnlyList<AdminOpsRunbookDto> GetAll() => RunbooksLazy.Value;

    public static AdminOpsRunbookDto? TryGetById(string runbookId) =>
        RunbooksLazy.Value.FirstOrDefault(runbook => runbook.Id == runbookId);

    private static IReadOnlyList<AdminOpsRunbookDto> LoadRunbooks()
    {
        using var stream = typeof(OpsRunbookCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded runbook catalog '{ResourceName}' was not found.");

        var document = JsonSerializer.Deserialize<RunbookDocument>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidOperationException("Runbook catalog JSON deserialized to null.");

        if (document.Runbooks.Count == 0)
        {
            throw new InvalidOperationException("Runbook catalog JSON must contain at least one runbook.");
        }

        var duplicateIds = document.Runbooks
            .GroupBy(runbook => runbook.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runbook catalog JSON contains duplicate ids: {string.Join(", ", duplicateIds)}.");
        }

        return document.Runbooks
            .Select(runbook =>
            {
                if (string.IsNullOrWhiteSpace(runbook.Id))
                {
                    throw new InvalidOperationException("Runbook catalog JSON contains a runbook with an empty id.");
                }

                if (runbook.Steps.Count == 0 || runbook.Steps.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException(
                        $"Runbook catalog JSON runbook '{runbook.Id}' must contain non-empty steps.");
                }

                return new AdminOpsRunbookDto(
                    runbook.Id.Trim(),
                    runbook.Owner.Trim(),
                    runbook.ResponseSla.Trim(),
                    runbook.Trigger.Trim(),
                    runbook.Steps.Select(step => step.Trim()).ToList());
            })
            .ToList();
    }

    private sealed record RunbookDocument(IReadOnlyList<RunbookEntry> Runbooks);

    private sealed record RunbookEntry(
        string Id,
        string Owner,
        string ResponseSla,
        string Trigger,
        IReadOnlyList<string> Steps);
}

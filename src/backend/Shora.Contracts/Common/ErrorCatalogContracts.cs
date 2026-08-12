namespace Shora.Contracts.Common;

public sealed record ErrorCatalogEntryResponse(
    string Code,
    int Status,
    string Title,
    string Summary,
    string Type,
    string? WhenItOccurs,
    string? RelatedEndpoint);

public sealed record ErrorCatalogListResponse(
    IReadOnlyList<ErrorCatalogEntryResponse> Items);

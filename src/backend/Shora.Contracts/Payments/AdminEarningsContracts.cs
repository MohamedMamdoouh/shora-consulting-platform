namespace Shora.Contracts.Payments;

public sealed record AdminEarningsQuery(DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed record AdminEarningsResponse(
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal NetRevenue,
    int ApprovedCount,
    int RefundedCount,
    int RefundDueCount);

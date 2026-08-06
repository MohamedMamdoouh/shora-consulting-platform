using Shora.Contracts.Payments;

namespace Shora.Application.Earnings;

public sealed class AdminEarningsQueryValidationResult
{
    private AdminEarningsQueryValidationResult(
        Dictionary<string, string[]> errors,
        ValidatedAdminEarningsQuery? value)
    {
        Errors = errors;
        Value = value;
    }

    public Dictionary<string, string[]> Errors { get; }

    public ValidatedAdminEarningsQuery? Value { get; }

    public bool IsValid => Errors.Count == 0 && Value is not null;

    public static AdminEarningsQueryValidationResult Success(ValidatedAdminEarningsQuery value) =>
        new([], value);

    public static AdminEarningsQueryValidationResult Failure(Dictionary<string, string[]> errors) =>
        new(errors, null);
}

public sealed record ValidatedAdminEarningsQuery(DateTime? FromUtc, DateTime? ToUtc);

public static class AdminEarningsQueryValidator
{
    public static AdminEarningsQueryValidationResult Validate(AdminEarningsQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        if (query.FromUtc is { } rawFromUtc)
        {
            fromUtc = NormalizeUtc(rawFromUtc);
        }

        if (query.ToUtc is { } rawToUtc)
        {
            toUtc = NormalizeUtc(rawToUtc);
        }

        if (fromUtc is not null && toUtc is not null && fromUtc >= toUtc)
        {
            AddError(errors, nameof(AdminEarningsQuery.ToUtc), "To must be later than from.");
        }

        if (errors.Count > 0)
        {
            return AdminEarningsQueryValidationResult.Failure(errors);
        }

        return AdminEarningsQueryValidationResult.Success(new ValidatedAdminEarningsQuery(fromUtc, toUtc));
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };

    private static void AddError(Dictionary<string, string[]> errors, string field, string message)
    {
        var key = char.ToLowerInvariant(field[0]) + field[1..];
        errors[key] = [message];
    }
}

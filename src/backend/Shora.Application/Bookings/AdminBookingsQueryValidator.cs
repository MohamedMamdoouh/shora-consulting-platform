using Shora.Contracts.Booking;

namespace Shora.Application.Bookings;

public sealed class AdminBookingsQueryValidationResult
{
    private AdminBookingsQueryValidationResult(
        Dictionary<string, string[]> errors,
        ValidatedAdminBookingsQuery? value)
    {
        Errors = errors;
        Value = value;
    }

    public Dictionary<string, string[]> Errors { get; }

    public ValidatedAdminBookingsQuery? Value { get; }

    public bool IsValid => Errors.Count == 0 && Value is not null;

    public static AdminBookingsQueryValidationResult Success(ValidatedAdminBookingsQuery value) =>
        new([], value);

    public static AdminBookingsQueryValidationResult Failure(Dictionary<string, string[]> errors) =>
        new(errors, null);
}

public sealed record ValidatedAdminBookingsQuery(
    AdminBookingStatusFilter? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public static class AdminBookingsQueryValidator
{
    public static AdminBookingsQueryValidationResult Validate(AdminBookingsQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (query.Page < AdminBookingsQueryLimits.DefaultPage)
        {
            AddError(errors, nameof(AdminBookingsQuery.Page), "Page must be at least 1.");
        }

        if (query.PageSize is < 1 or > AdminBookingsQueryLimits.MaxPageSize)
        {
            AddError(
                errors,
                nameof(AdminBookingsQuery.PageSize),
                $"Page size must be between 1 and {AdminBookingsQueryLimits.MaxPageSize}.");
        }

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
            AddError(errors, nameof(AdminBookingsQuery.ToUtc), "To must be later than from.");
        }

        if (errors.Count > 0)
        {
            return AdminBookingsQueryValidationResult.Failure(errors);
        }

        return AdminBookingsQueryValidationResult.Success(
            new ValidatedAdminBookingsQuery(query.Status, fromUtc, toUtc, query.Page, query.PageSize));
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

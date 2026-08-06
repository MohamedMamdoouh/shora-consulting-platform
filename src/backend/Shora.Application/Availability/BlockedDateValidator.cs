using Shora.Contracts.Availability;

namespace Shora.Application.Availability;

public sealed class BlockedDateValidationResult
{
    private BlockedDateValidationResult(
        Dictionary<string, string[]> errors,
        ValidatedBlockedDate? value)
    {
        Errors = errors;
        Value = value;
    }

    public Dictionary<string, string[]> Errors { get; }

    public ValidatedBlockedDate? Value { get; }

    public bool IsValid => Errors.Count == 0 && Value is not null;

    public static BlockedDateValidationResult Success(ValidatedBlockedDate value) =>
        new([], value);

    public static BlockedDateValidationResult Failure(Dictionary<string, string[]> errors) =>
        new(errors, null);
}

public sealed record ValidatedBlockedDate(DateTime StartUtc, DateTime EndUtc, string? Reason);

public static class BlockedDateValidator
{
    private const int MaxReasonLength = 500;

    public static BlockedDateValidationResult ValidateCreate(CreateBlockedDateRequest request) =>
        Validate(request.StartUtc, request.EndUtc, request.Reason);

    private static BlockedDateValidationResult Validate(DateTime startUtc, DateTime endUtc, string? reason)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        startUtc = NormalizeUtc(startUtc);
        endUtc = NormalizeUtc(endUtc);

        if (startUtc >= endUtc)
        {
            AddError(errors, nameof(CreateBlockedDateRequest.EndUtc), "End time must be later than start time.");
        }

        if (reason is not null && reason.Length > MaxReasonLength)
        {
            AddError(
                errors,
                nameof(CreateBlockedDateRequest.Reason),
                $"Reason must be at most {MaxReasonLength} characters.");
        }

        if (errors.Count > 0)
        {
            return BlockedDateValidationResult.Failure(errors);
        }

        return BlockedDateValidationResult.Success(new ValidatedBlockedDate(startUtc, endUtc, reason));
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

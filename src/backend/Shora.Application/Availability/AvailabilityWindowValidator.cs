using Shora.Contracts.Availability;

namespace Shora.Application.Availability;

public sealed class AvailabilityWindowValidationResult
{
    private AvailabilityWindowValidationResult(
        Dictionary<string, string[]> errors,
        ValidatedAvailabilityWindow? value)
    {
        Errors = errors;
        Value = value;
    }

    public Dictionary<string, string[]> Errors { get; }

    public ValidatedAvailabilityWindow? Value { get; }

    public bool IsValid => Errors.Count == 0 && Value is not null;

    public static AvailabilityWindowValidationResult Success(ValidatedAvailabilityWindow value) =>
        new([], value);

    public static AvailabilityWindowValidationResult Failure(Dictionary<string, string[]> errors) =>
        new(errors, null);
}

public sealed record ValidatedAvailabilityWindow(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive);

public static class AvailabilityWindowValidator
{
    public static AvailabilityWindowValidationResult ValidateCreate(CreateAvailabilityWindowRequest request) =>
        Validate(request.DayOfWeek, request.StartTime, request.EndTime, request.IsActive);

    public static AvailabilityWindowValidationResult ValidateUpdate(UpdateAvailabilityWindowRequest request) =>
        Validate(request.DayOfWeek, request.StartTime, request.EndTime, request.IsActive);

    private static AvailabilityWindowValidationResult Validate(
        DayOfWeek dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        bool isActive)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!Enum.IsDefined(dayOfWeek))
        {
            AddError(errors, nameof(CreateAvailabilityWindowRequest.DayOfWeek), "Day of week is invalid.");
        }

        if (startTime < TimeSpan.Zero || endTime < TimeSpan.Zero)
        {
            AddError(errors, nameof(CreateAvailabilityWindowRequest.StartTime), "Times must not be negative.");
        }

        if (startTime >= endTime)
        {
            AddError(
                errors,
                nameof(CreateAvailabilityWindowRequest.EndTime),
                "End time must be later than start time.");
        }

        if (errors.Count > 0)
        {
            return AvailabilityWindowValidationResult.Failure(errors);
        }

        return AvailabilityWindowValidationResult.Success(
            new ValidatedAvailabilityWindow(dayOfWeek, startTime, endTime, isActive));
    }

    private static void AddError(Dictionary<string, string[]> errors, string field, string message)
    {
        var key = char.ToLowerInvariant(field[0]) + field[1..];
        errors[key] = [message];
    }
}

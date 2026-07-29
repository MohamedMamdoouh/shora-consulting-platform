using Shora.Application.Common;
using Shora.Application.Common.Results;

namespace Shora.Application.Availability;

public static class AvailabilityRangeValidator
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(SlotGenerationConstants.HorizonWeeks * 7);

    public static Result<(DateTime FromUtc, DateTime ToUtc)> Normalize(
        DateTime fromUtc,
        DateTime toUtc,
        DateTime utcNow)
    {
        fromUtc = NormalizeToUtc(fromUtc);
        toUtc = NormalizeToUtc(toUtc);

        if (fromUtc >= toUtc)
        {
            return Result<(DateTime, DateTime)>.Failure(
                Error.Validation(
                    ErrorCodes.Availability.InvalidRange,
                    "The availability range is invalid: 'from' must be earlier than 'to'."));
        }

        if (toUtc - fromUtc > MaxRange)
        {
            return Result<(DateTime, DateTime)>.Failure(
                Error.Validation(
                    ErrorCodes.Availability.RangeTooLarge,
                    $"The availability range must not exceed {SlotGenerationConstants.HorizonWeeks} weeks."));
        }

        var effectiveFromUtc = fromUtc < utcNow ? utcNow : fromUtc;
        return Result<(DateTime, DateTime)>.Success((effectiveFromUtc, toUtc));
    }

    private static DateTime NormalizeToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

namespace Shora.Application.Availability;

public static class SlotScheduleCalculator
{
    public static IReadOnlyList<SlotInterval> GenerateDesiredSlots(
        IReadOnlyList<AvailabilityWindowSpec> windows,
        IReadOnlyList<BlockedRangeSpec> blockedRanges,
        DateTime horizonStartUtc,
        DateTime horizonEndUtc,
        int sessionDurationMinutes,
        int bufferMinutes,
        TimeZoneInfo consultantTimeZone)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sessionDurationMinutes);

        ArgumentOutOfRangeException.ThrowIfNegative(bufferMinutes);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(horizonStartUtc, horizonEndUtc);

        var sessionDuration = TimeSpan.FromMinutes(sessionDurationMinutes);
        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        var desired = new List<SlotInterval>();

        var localStartDate = TimeZoneInfo.ConvertTimeFromUtc(horizonStartUtc, consultantTimeZone).Date;
        var localEndDate = TimeZoneInfo.ConvertTimeFromUtc(horizonEndUtc, consultantTimeZone).Date;

        for (var date = localStartDate; date <= localEndDate; date = date.AddDays(1))
        {
            foreach (var window in windows.Where(w => w.IsActive && w.DayOfWeek == date.DayOfWeek))
            {
                var windowStartLocal = date + window.StartTime;
                var windowEndLocal = date + window.EndTime;

                if (windowEndLocal <= windowStartLocal)
                {
                    continue;
                }

                var windowStartUtc = ConvertConsultantLocalToUtc(windowStartLocal, consultantTimeZone);
                var windowEndUtc = ConvertConsultantLocalToUtc(windowEndLocal, consultantTimeZone);

                desired.AddRange(
                    PackWindow(windowStartUtc, windowEndUtc, sessionDuration, buffer, blockedRanges, horizonStartUtc, horizonEndUtc));
            }
        }

        return desired
            .DistinctBy(slot => slot.StartUtc)
            .OrderBy(slot => slot.StartUtc)
            .ToList();
    }

    public static bool OverlapsBlockedRange(DateTime startUtc, DateTime endUtc, IReadOnlyList<BlockedRangeSpec> blockedRanges) =>
        blockedRanges.Any(block => startUtc < block.EndUtc && endUtc > block.StartUtc);

    private static IEnumerable<SlotInterval> PackWindow(
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        TimeSpan sessionDuration,
        TimeSpan buffer,
        IReadOnlyList<BlockedRangeSpec> blockedRanges,
        DateTime horizonStartUtc,
        DateTime horizonEndUtc)
    {
        var cursor = windowStartUtc;

        while (cursor + sessionDuration <= windowEndUtc)
        {
            var slotEnd = cursor + sessionDuration;

            if (cursor >= horizonStartUtc
                && cursor < horizonEndUtc
                && !OverlapsBlockedRange(cursor, slotEnd, blockedRanges))
            {
                yield return new SlotInterval(cursor, slotEnd);
            }

            cursor = slotEnd + buffer;
        }
    }

    private static DateTime ConvertConsultantLocalToUtc(DateTime localDateTime, TimeZoneInfo consultantTimeZone)
    {
        if (consultantTimeZone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddHours(1);
        }

        if (consultantTimeZone.IsAmbiguousTime(localDateTime))
        {
            var offsets = consultantTimeZone.GetAmbiguousTimeOffsets(localDateTime);
            var earliestOffset = offsets.Min();
            return new DateTimeOffset(localDateTime, earliestOffset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, consultantTimeZone);
    }
}

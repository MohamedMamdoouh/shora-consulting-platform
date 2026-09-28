using Shora.Application.Availability;

namespace Shora.Tests.Unit.Availability;

public class SlotScheduleCalculatorTests
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    [Fact]
    public void GenerateDesiredSlots_packs_session_and_buffer_within_window()
    {
        var windows = new[]
        {
            new AvailabilityWindowSpec(DayOfWeek.Monday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0))
        };

        var horizonStartUtc = new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
        var horizonEndUtc = horizonStartUtc.AddDays(1);

        var slots = SlotScheduleCalculator.GenerateDesiredSlots(
            windows,
            [],
            horizonStartUtc,
            horizonEndUtc,
            sessionDurationMinutes: 60,
            bufferMinutes: 15,
            Cairo);

        Assert.Equal(4, slots.Count);

        for (var index = 1; index < slots.Count; index++)
        {
            var gap = slots[index].StartUtc - slots[index - 1].EndUtc;
            Assert.Equal(TimeSpan.FromMinutes(15), gap);
        }
    }

    [Fact]
    public void GenerateDesiredSlots_skips_blocked_ranges()
    {
        var windows = new[]
        {
            new AvailabilityWindowSpec(DayOfWeek.Monday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0))
        };

        var horizonStartUtc = new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
        var horizonEndUtc = horizonStartUtc.AddDays(1);

        var slotsWithoutBlock = SlotScheduleCalculator.GenerateDesiredSlots(
            windows,
            [],
            horizonStartUtc,
            horizonEndUtc,
            sessionDurationMinutes: 60,
            bufferMinutes: 15,
            Cairo);

        var blockedStart = slotsWithoutBlock[1].StartUtc;
        var blockedEnd = slotsWithoutBlock[1].EndUtc;
        var blockedRanges = new[] { new BlockedRangeSpec(blockedStart, blockedEnd) };

        var slots = SlotScheduleCalculator.GenerateDesiredSlots(
            windows,
            blockedRanges,
            horizonStartUtc,
            horizonEndUtc,
            sessionDurationMinutes: 60,
            bufferMinutes: 15,
            Cairo);

        Assert.DoesNotContain(slots, slot => slot.StartUtc == blockedStart);
        Assert.Equal(slotsWithoutBlock.Count - 1, slots.Count);
    }

    [Fact]
    public void GenerateDesiredSlots_is_idempotent_for_same_inputs()
    {
        var windows = new[]
        {
            new AvailabilityWindowSpec(DayOfWeek.Sunday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
            new AvailabilityWindowSpec(DayOfWeek.Monday, new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0)),
        };

        var horizonStartUtc = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
        var horizonEndUtc = horizonStartUtc.AddDays(7);

        var firstRun = SlotScheduleCalculator.GenerateDesiredSlots(
            windows,
            [],
            horizonStartUtc,
            horizonEndUtc,
            sessionDurationMinutes: 60,
            bufferMinutes: 15,
            Cairo);

        var secondRun = SlotScheduleCalculator.GenerateDesiredSlots(
            windows,
            [],
            horizonStartUtc,
            horizonEndUtc,
            sessionDurationMinutes: 60,
            bufferMinutes: 15,
            Cairo);

        Assert.Equal(firstRun.Select(slot => slot.StartUtc), secondRun.Select(slot => slot.StartUtc));
    }

    [Fact]
    public void OverlapsBlockedRange_detects_partial_overlap()
    {
        var blocked = new[] { new BlockedRangeSpec(new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc)) };

        Assert.True(SlotScheduleCalculator.OverlapsBlockedRange(
            new DateTime(2026, 7, 27, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc),
            blocked));

        Assert.False(SlotScheduleCalculator.OverlapsBlockedRange(
            new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc),
            blocked));
    }
}

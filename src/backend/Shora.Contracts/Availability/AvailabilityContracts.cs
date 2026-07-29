namespace Shora.Contracts.Availability;

public sealed record AvailabilitySlotDto(
    Guid Id,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);

public sealed record AvailabilityResponse(IReadOnlyList<AvailabilitySlotDto> Slots);

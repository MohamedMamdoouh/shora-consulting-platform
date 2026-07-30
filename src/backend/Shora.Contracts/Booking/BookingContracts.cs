using System.Text.Json.Serialization;

namespace Shora.Contracts.Booking;

public enum DeliveryMethod
{
    VoiceCall = 0,
    Chat = 1
}

public enum CancellationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Declined = 2,
    AutoDeclined = 3
}

public sealed record CreateBookingRequest(
    Guid AvailabilitySlotId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DeliveryMethod>))]
    DeliveryMethod DeliveryMethod,
    string? ContactPhone);

public sealed record ReserveBookingResponse(
    Guid BookingId,
    PaymentInstructionsSnapshot PaymentInstructions);

public sealed record PaymentInstructionsSnapshot(
    decimal Amount,
    string Currency,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions,
    DateTime ReceiptUploadDeadlineUtc);

public sealed record CancellationRequestBody(string? Reason);

public sealed record CancellationRequestResponse(
    Guid RequestId,
    CancellationRequestStatus Status,
    DateTime AutoDeclineAtUtc,
    string BookingStatus);

using Shora.Domain.Constants;

namespace Shora.Application.Options;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public int UnconfirmedHoldCap { get; set; } = SettingsDefaults.UnconfirmedHoldCap;
}

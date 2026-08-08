using Microsoft.Extensions.Logging;

namespace Shora.Application.Common;

public static class PaymentLogScope
{
    public static IDisposable? Begin(
        ILogger logger,
        Guid bookingId,
        Guid? paymentId = null,
        Guid? receiptId = null)
    {
        var state = new Dictionary<string, object> { ["BookingId"] = bookingId };

        if (paymentId.HasValue)
        {
            state["PaymentId"] = paymentId.Value;
        }

        if (receiptId.HasValue)
        {
            state["ReceiptId"] = receiptId.Value;
        }

        return logger.BeginScope(state);
    }
}

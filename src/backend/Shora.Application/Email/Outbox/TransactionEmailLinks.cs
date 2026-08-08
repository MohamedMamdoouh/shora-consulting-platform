using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Application.Email.Outbox;

public sealed class TransactionEmailLinks(IOptions<FrontendOptions> frontendOptions)
{
    private readonly FrontendOptions _frontend = frontendOptions.Value;

    public string ClientDashboard() => Build("/dashboard");

    public string AdminBookings() => Build("/admin/bookings");

    public string ClientPayment(Guid bookingId) => Build($"/booking/payment/{bookingId:D}");

    private string Build(string path) =>
        $"{_frontend.BaseUrl.TrimEnd('/')}{path}";
}

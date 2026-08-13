using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class BrevoEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private const string EmailsEndpoint = "https://api.brevo.com/v3/smtp/email";
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Brevo email is not configured. Set Email:ApiKey and Email:FromAddress.");
        }

        var payload = new BrevoEmailRequest(
            new BrevoSender(_options.FromAddress, string.IsNullOrWhiteSpace(_options.FromName) ? null : _options.FromName),
            [new BrevoRecipient(toEmail)],
            subject,
            htmlBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, EmailsEndpoint);
        request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                BuildFailureMessage(response.StatusCode, body, _options.FromAddress));
        }

        logger.LogInformation("Transactional email sent with subject {Subject}.", subject);
    }

    internal static string BuildFailureMessage(
        System.Net.HttpStatusCode statusCode,
        string responseBody,
        string configuredFromAddress)
    {
        var brevoMessage = TryReadBrevoErrorMessage(responseBody);
        var message = brevoMessage is null
            ? $"Brevo API returned {(int)statusCode}: {responseBody}"
            : $"Brevo API returned {(int)statusCode}: {brevoMessage}";

        if (statusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Forbidden)
        {
            message +=
                $" Current Email:FromAddress is '{configuredFromAddress}'. " +
                "Verify the sender at https://app.brevo.com/senders/list and ensure it matches exactly.";
        }

        return message;
    }

    private static string? TryReadBrevoErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private sealed record BrevoEmailRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyList<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent);

    private sealed record BrevoSender(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email);
}

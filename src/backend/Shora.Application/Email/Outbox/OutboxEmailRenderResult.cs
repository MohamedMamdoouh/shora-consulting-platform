namespace Shora.Application.Email.Outbox;

public sealed record OutboxEmailRenderResult(
    string ToEmail,
    string Subject,
    string HtmlBody);

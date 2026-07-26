namespace Shora.Application.Email;

public sealed record EmailTemplateRequest(
    string ContentTemplate,
    string PreviewText,
    string Heading,
    string ActionUrl,
    string ActionLabel,
    string RecipientName,
    string FooterNote);

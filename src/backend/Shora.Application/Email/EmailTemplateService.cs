using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Application.Email;

public sealed partial class EmailTemplateService(IOptions<EmailBrandOptions> brandOptions) : IEmailTemplateService
{
    private const string ResourcePrefix = "Shora.Application.Email.Templates.";
    private const string LayoutTemplate = "_layout.html";

    private static readonly Assembly TemplateAssembly = typeof(EmailTemplateService).Assembly;
    private static readonly ConcurrentDictionary<string, string> TemplateCache = new(StringComparer.Ordinal);

    private readonly EmailBrandOptions _brand = brandOptions.Value;

    public string Render(EmailTemplateRequest request)
    {
        var tokens = BuildTokens(request);
        tokens["Content"] = ReplaceTokens(LoadTemplate(request.ContentTemplate), tokens);
        return ReplaceTokens(LoadTemplate(LayoutTemplate), tokens);
    }

    private Dictionary<string, string> BuildTokens(EmailTemplateRequest request)
    {
        var brandName = HtmlEncode(_brand.BrandName);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RecipientName"] = HtmlEncode(request.RecipientName),
            ["BrandName"] = brandName,
            ["Heading"] = HtmlEncode(request.Heading),
            ["PreviewText"] = HtmlEncode(request.PreviewText),
            ["ActionUrl"] = request.ActionUrl,
            ["ActionLabel"] = HtmlEncode(request.ActionLabel),
            ["FooterNote"] = HtmlEncode(request.FooterNote),
            ["BrandHeader"] = $"""<p style="margin:0;font-size:20px;font-weight:700;color:#b85c38;">{brandName}</p>""",
            ["Year"] = DateTime.UtcNow.Year.ToString()
        };
    }

    private static string LoadTemplate(string templatePath)
    {
        var resourceKey = templatePath.Replace('/', '.').Replace('\\', '.');
        return TemplateCache.GetOrAdd(resourceKey, static key =>
        {
            var resourceName = ResourcePrefix + key;
            using var stream = TemplateAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Email template '{key}' was not found.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> tokens) =>
        PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!tokens.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException(
                    $"Email template placeholder '{{{{{name}}}}}' has no replacement value.");
            }

            return value;
        });

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);

    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}")]
    private static partial Regex PlaceholderPattern();
}

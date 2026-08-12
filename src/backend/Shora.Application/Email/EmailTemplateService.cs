using System.Net;
using System.Reflection;
using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Application.Email;

public sealed class EmailTemplateService(IOptions<EmailBrandOptions> brandOptions) : IEmailTemplateService
{
    private const string ResourcePrefix = "Shora.Application.Email.Templates.";
    private const string LayoutTemplate = "_layout.html";
    private const string BrandPrimaryColor = "#4a5748";
    private const string BrandAccentColor = "#7a6250";
    private const string BrandTextColor = "#1a1816";

    private static readonly Assembly TemplateAssembly = typeof(EmailTemplateService).Assembly;

    private readonly EmailBrandOptions _brand = brandOptions.Value;

    public string Render(EmailTemplateRequest request)
    {
        var tokens = BuildTokens(request);
        tokens["Content"] = ReplaceTokens(LoadTemplate(request.ContentTemplate), tokens);
        return ReplaceTokens(LoadTemplate(LayoutTemplate), tokens);
    }

    internal static string RenderFragment(string templatePath, IReadOnlyDictionary<string, string> tokens) =>
        ReplaceTokens(LoadTemplate(templatePath), tokens);

    private Dictionary<string, string> BuildTokens(EmailTemplateRequest request)
    {
        var brandName = HtmlEncode(_brand.BrandName);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RecipientName"] = HtmlEncode(request.RecipientName),
            ["BrandName"] = brandName,
            ["Heading"] = HtmlEncode(request.Heading),
            ["PreviewText"] = HtmlEncode(request.PreviewText),
            ["ActionUrl"] = request.ActionUrl,
            ["ActionLabel"] = HtmlEncode(request.ActionLabel),
            ["FooterNote"] = HtmlEncode(request.FooterNote),
            ["BrandHeader"] = BuildBrandHeader(brandName),
            ["Year"] = DateTime.UtcNow.Year.ToString()
        };

        if (request.AdditionalTokens is not null)
        {
            foreach (var (key, value) in request.AdditionalTokens)
            {
                tokens[key] = value;
            }
        }

        return tokens;
    }

    private static string BuildBrandHeader(string brandName) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto;">
          <tr>
            <td style="padding:0 0 0 10px;font-size:22px;line-height:1;font-weight:700;color:{BrandTextColor};font-family:Georgia,'Times New Roman',serif;">
              {brandName}
            </td>
            <td style="padding:0;line-height:0;">
              <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" role="img" aria-label="{brandName}">
                <path d="M6 22 Q6 10 12 5" fill="none" stroke="{BrandAccentColor}" stroke-width="2.3" stroke-linecap="round"/>
                <path d="M18 22 Q18 10 12 5" fill="none" stroke="{BrandPrimaryColor}" stroke-width="2.3" stroke-linecap="round"/>
                <circle cx="12" cy="21" r="2" fill="{BrandPrimaryColor}"/>
              </svg>
            </td>
          </tr>
        </table>
        """;

    private static string LoadTemplate(string templatePath)
    {
        var resourceName = ResourcePrefix + templatePath.Replace('/', '.').Replace('\\', '.');
        using var stream = TemplateAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{templatePath}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
        {
            template = template.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        }

        return template;
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}

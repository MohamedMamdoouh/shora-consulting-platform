using System.Net;
using System.Reflection;
using Microsoft.Extensions.Options;
using Shora.Application.Options;

namespace Shora.Application.Email;

public sealed class EmailTemplateService(IOptions<EmailBrandOptions> brandOptions) : IEmailTemplateService
{
    private const string ResourcePrefix = "Shora.Application.Email.Templates.";
    private const string LogoResourceName = "Shora.Application.Email.Assets.logo.png";
    private const string LayoutTemplate = "_layout.html";
    private const string BrandTextColor = "#1a1816";

    private static readonly Assembly TemplateAssembly = typeof(EmailTemplateService).Assembly;
    private static readonly Lazy<string> LogoDataUri = new(LoadLogoDataUri);

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
            <td style="padding:0 0 0 10px;line-height:0;">
              <img src="{LogoDataUri.Value}" width="40" height="40" alt="{brandName}" style="display:block;border:0;" />
            </td>
            <td style="padding:0;font-size:22px;line-height:1;font-weight:700;color:{BrandTextColor};font-family:Georgia,'Times New Roman',serif;">
              {brandName}
            </td>
          </tr>
        </table>
        """;

    private static string LoadLogoDataUri()
    {
        using var stream = TemplateAssembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException($"Logo asset '{LogoResourceName}' was not found.");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return $"data:image/png;base64,{Convert.ToBase64String(memoryStream.ToArray())}";
    }

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

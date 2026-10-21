using System.Net;

namespace RealEstateERP.Core.Common;

/// <summary>Simple branded HTML emails with inline styles (what email clients reliably render).</summary>
public static class EmailTemplates
{
    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string Render(string platformName, string heading, IEnumerable<string> paragraphs, string? buttonText = null, string? buttonUrl = null, string? footnote = null)
        => Frame(platformName, heading, Paragraphs(paragraphs) + Button(buttonText, buttonUrl) + Footnote(footnote));

    /// <summary>Like Render, with a two-column table of label/value rows (e.g. invoice lines) after the paragraphs.</summary>
    public static string RenderWithRows(string platformName, string heading, IEnumerable<string> paragraphs,
        IEnumerable<(string Label, string Value, bool Strong)> rows, string? afterTable = null, string? footnote = null)
    {
        var table = "<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin:6px 0 16px\">" +
            string.Concat(rows.Select(r =>
            {
                var weight = r.Strong ? ";font-weight:700" : "";
                return $"<tr><td style=\"padding:6px 0;border-bottom:1px solid #dfe5ee{weight}\">{E(r.Label)}</td>" +
                       $"<td style=\"padding:6px 0;border-bottom:1px solid #dfe5ee;text-align:right;white-space:nowrap{weight}\">{E(r.Value)}</td></tr>";
            })) + "</table>";
        var after = string.IsNullOrWhiteSpace(afterTable) ? "" :
            $"<div style=\"background:#f3f6fa;border-radius:8px;padding:12px 14px;margin:0 0 14px;white-space:pre-line;line-height:1.5\">{E(afterTable)}</div>";
        return Frame(platformName, heading, Paragraphs(paragraphs) + table + after + Footnote(footnote));
    }

    /// <summary>Wraps HTML produced elsewhere (e.g. ASP.NET Identity's built-in pages) in the same frame. Not re-encoded.</summary>
    public static string Wrap(string platformName, string subject, string trustedHtml)
        => Frame(platformName, subject, $"<div style=\"line-height:1.5\">{trustedHtml}</div>");

    private static string Paragraphs(IEnumerable<string> paragraphs)
        => string.Concat(paragraphs.Select(p => $"<p style=\"margin:0 0 14px;line-height:1.5\">{E(p)}</p>"));

    private static string Button(string? text, string? url)
        => text is null || url is null ? "" :
            $"<p style=\"margin:22px 0\"><a href=\"{E(url)}\" style=\"background:#1554a8;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:6px;font-weight:600;display:inline-block\">{E(text)}</a></p>" +
            $"<p style=\"margin:0 0 14px;font-size:12px;color:#657489\">If the button doesn't work, copy this link into your browser:<br><span style=\"word-break:break-all\">{E(url)}</span></p>";

    private static string Footnote(string? text)
        => text is null ? "" : $"<p style=\"margin:18px 0 0;font-size:12px;color:#657489\">{E(text)}</p>";

    /// <summary>The shared frame. Heading and platform name are encoded here; innerHtml is already safe.</summary>
    private static string Frame(string platformName, string heading, string innerHtml)
        => "<div style=\"background:#f3f6fa;padding:24px 12px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f1d2e\">" +
           "<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #dfe5ee;border-radius:10px;padding:28px\">" +
           $"<div style=\"font-weight:700;color:#1554a8;margin-bottom:18px\">{E(platformName)}</div>" +
           $"<h1 style=\"font-size:20px;margin:0 0 16px\">{E(heading)}</h1>" +
           innerHtml +
           "</div></div>";
}

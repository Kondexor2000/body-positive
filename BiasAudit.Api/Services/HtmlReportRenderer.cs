using System.Net;
using System.Text;
using BiasAudit.Api.Models;

namespace BiasAudit.Api.Services;

public interface IReportRenderer
{
    string Render(AuditJob job);
}

public sealed class HtmlReportRenderer : IReportRenderer
{
    public string Render(AuditJob job)
    {
        var detections = job.GetDetections();
        var findings = job.GetFindings();
        var riskPercent = Math.Round(job.BiasRiskScore * 100, 0);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pl\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>Raport audytu biasu</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Inter,Segoe UI,Arial,sans-serif;margin:0;background:#f7f4ef;color:#1d2521}main{max-width:960px;margin:0 auto;padding:40px 24px}.hero{border-bottom:4px solid #244f46;padding-bottom:24px}.meta,.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px}.tile{background:#fff;border:1px solid #dfd8ce;border-radius:8px;padding:16px}h1,h2{letter-spacing:0}.score{font-size:44px;font-weight:800;color:#244f46}.finding{margin:14px 0;padding:16px;border-left:6px solid #9b6a3c;background:#fff}.High{border-left-color:#b42318}.Medium{border-left-color:#b7791f}.Low{border-left-color:#2f855a}.Info{border-left-color:#2b6cb0}table{width:100%;border-collapse:collapse;background:#fff}th,td{text-align:left;border-bottom:1px solid #e7dfd4;padding:10px}footer{margin-top:32px;color:#59645e;font-size:14px}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body><main>");
        html.AppendLine("<section class=\"hero\">");
        html.AppendLine("<h1>Raport audytu biasu w modelu moderacji</h1>");
        html.AppendLine("<p>Raport wspiera analizę body-positive: oddziela sygnały techniczne od ocen wartościujących i oznacza ryzyka nadmiernej seksualizacji neutralnych reprezentacji ciała.</p>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"grid\">");
        html.AppendLine(Tile("Id audytu", job.Id.ToString()));
        html.AppendLine(Tile("Plik", job.OriginalFileName));
        html.AppendLine(Tile("Decyzja modelu", job.ModelDecision ?? "nie podano"));
        html.AppendLine(Tile("Kohorta", job.Cohort ?? "nie podano"));
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"tile\">");
        html.AppendLine("<h2>Wynik ryzyka</h2>");
        html.AppendLine($"<div class=\"score\">{riskPercent}%</div>");
        html.AppendLine("<p>Wyższy wynik oznacza większą potrzebę przeglądu człowieka, testów kohortowych i korekty polityk moderacji.</p>");
        html.AppendLine("</section>");
        html.AppendLine("<section>");
        html.AppendLine("<h2>Ustalenia</h2>");
        foreach (var finding in findings)
        {
            html.AppendLine($"<article class=\"finding {Encode(finding.Severity)}\">");
            html.AppendLine($"<strong>{Encode(finding.Severity)}: {Encode(finding.Title)}</strong>");
            html.AppendLine($"<p>{Encode(finding.Description)}</p>");
            html.AppendLine($"<p><b>Rekomendacja:</b> {Encode(finding.Recommendation)}</p>");
            html.AppendLine("</article>");
        }
        html.AppendLine("</section>");
        html.AppendLine("<section>");
        html.AppendLine("<h2>Detekcje NudeNet</h2>");
        html.AppendLine("<table><thead><tr><th>Etykieta</th><th>Pewność</th><th>Ramka</th></tr></thead><tbody>");
        foreach (var detection in detections)
        {
            var box = detection.Box is null
                ? "brak"
                : $"{detection.Box.X}, {detection.Box.Y}, {detection.Box.Width}, {detection.Box.Height}";
            html.AppendLine($"<tr><td>{Encode(detection.Label)}</td><td>{detection.Confidence:P0}</td><td>{Encode(box)}</td></tr>");
        }
        if (detections.Count == 0)
        {
            html.AppendLine("<tr><td colspan=\"3\">Brak detekcji lub użyty tryb mock.</td></tr>");
        }
        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
        html.AppendLine("<footer>Raport nie zastępuje ewaluacji etycznej, testów reprezentatywności ani procedury odwoławczej dla osób dotkniętych moderacją.</footer>");
        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static string Tile(string label, string value) =>
        $"<div class=\"tile\"><b>{Encode(label)}</b><p>{Encode(value)}</p></div>";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

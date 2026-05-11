using BiasAudit.Api.Models;

namespace BiasAudit.Api.Services;

public static class AuditScoring
{
    private static readonly string[] ExplicitLabels =
    [
        "EXPOSED_BREAST_F",
        "EXPOSED_GENITALIA_F",
        "EXPOSED_GENITALIA_M",
        "EXPOSED_ANUS",
        "EXPOSED_BUTTOCKS"
    ];

    private static readonly string[] ContextLabels =
    [
        "BELLY_EXPOSED",
        "ARMPITS_EXPOSED",
        "FEET_EXPOSED",
        "FACE_F",
        "FACE_M"
    ];

    public static AuditFindingSet Score(AuditJob job, IReadOnlyCollection<NudeNetDetection> detections)
    {
        var findings = new List<AuditFinding>
        {
            new(
                "Info",
                "Zakres audytu",
                "Raport sprawdza ryzyko nadmiernej seksualizacji lub nierównego traktowania treści ciałopozytywnych. Nie ocenia wyglądu osoby i nie wnioskuje o tożsamości.",
                "Łącz wynik z przeglądem ludzkim i dobrowolnie podanymi metadanymi kohorty.")
        };

        var explicitScore = detections
            .Where(x => ExplicitLabels.Contains(x.Label, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Confidence)
            .DefaultIfEmpty(0)
            .Max();

        var contextOnly = detections.Count > 0 &&
            explicitScore < 0.55m &&
            detections.Any(x => ContextLabels.Contains(x.Label, StringComparer.OrdinalIgnoreCase));

        var decision = job.ModelDecision?.ToLowerInvariant();
        var rejected = decision is "rejected" or "reject" or "blocked" or "hidden" or "odrzucony" or "zablokowany";
        var approved = decision is "approved" or "approve" or "visible" or "accepted" or "zaakceptowany";

        decimal risk = explicitScore switch
        {
            >= 0.80m => 0.20m,
            >= 0.55m => 0.35m,
            _ => 0.45m
        };

        if (contextOnly)
        {
            risk += 0.25m;
            findings.Add(new(
                "Medium",
                "Kontekst ciała bez jednoznacznej nagości",
                "NudeNet zwrócił etykiety kontekstowe przy niskiej pewności etykiet eksplicytnych. Taki przypadek bywa podatny na nadmoderację zdjęć plażowych, sportowych, medycznych lub plus-size.",
                "Nie blokuj automatycznie na podstawie samych etykiet kontekstowych. Wymagaj progu pewności, kontekstu i ścieżki odwołania."));
        }

        if (rejected && explicitScore < 0.55m)
        {
            risk += 0.25m;
            findings.Add(new(
                "High",
                "Decyzja modelu wygląda ostrzej niż sygnał NudeNet",
                "Przekazana decyzja modelu oznacza odrzucenie, ale najwyższy sygnał eksplicytny NudeNet jest niski. To wskazuje na ryzyko biasu przeciw neutralnym reprezentacjom ciała.",
                "Przejrzyj reguły blokowania i porównaj false positive rate między kohortami podanymi dobrowolnie."));
        }

        if (approved && explicitScore >= 0.80m)
        {
            risk += 0.10m;
            findings.Add(new(
                "Low",
                "Wysoki sygnał eksplicytny przy decyzji aprobującej",
                "Decyzja aprobująca nie jest sama w sobie błędem, ale wymaga spójnej polityki treści i jasnego rozdzielenia nagości od body-positive.",
                "Udokumentuj kryteria, aby nie karać edukacyjnych, zdrowotnych lub afirmujących reprezentacji ciała."));
        }

        if (!string.IsNullOrWhiteSpace(job.Cohort))
        {
            findings.Add(new(
                "Info",
                "Kohorta podana przez użytkownika",
                $"Raport uwzględnia dobrowolną kohortę: {job.Cohort}. System nie próbuje jej odgadnąć z obrazu.",
                "Analizuj kohorty zagregowanie i unikaj decyzji personalnych opartych na cechach wrażliwych."));
        }

        return new(Math.Clamp(risk, 0, 1), findings);
    }
}

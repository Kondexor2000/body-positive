namespace BiasAudit.Api.Models;

public sealed record UploadAuditResponse(Guid Id, string Status);

public sealed record AuditStatusResponse(
    Guid Id,
    string Status,
    string OriginalFileName,
    decimal BiasRiskScore,
    string? ModelDecision,
    string? Cohort,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    IReadOnlyCollection<AuditFinding> Findings)
{
    public static AuditStatusResponse From(AuditJob job) =>
        new(
            job.Id,
            job.Status.ToString(),
            job.OriginalFileName,
            job.BiasRiskScore,
            job.ModelDecision,
            job.Cohort,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.Error,
            job.GetFindings());
}

public sealed record ProblemDetailsResponse(string Message);

public sealed record HealthResponse(string status, string service);

public sealed record NudeNetDetection(string Label, decimal Confidence, BoundingBox? Box);

public sealed record BoundingBox(decimal X, decimal Y, decimal Width, decimal Height);

public sealed record AuditFinding(string Severity, string Title, string Description, string Recommendation);

public sealed record AuditFindingSet(decimal Score, IReadOnlyCollection<AuditFinding> Findings);

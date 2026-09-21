using System.Text.Json;

namespace BiasAudit.Api.Models;

public sealed class AuditJob
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // OWNER
    public Guid UserId { get; private set; }

    public User User { get; private set; } = default!;

    // STATUS
    public AuditStatus Status { get; private set; } = AuditStatus.Queued;

    // FILE
    public string ObjectKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    // OPTIONAL USER DATA
    public string? ModelDecision { get; private set; }

    public string? Cohort { get; private set; }

    public string? Notes { get; private set; }

    // RESULTS
    public string NudeNetJson { get; private set; } = "[]";

    public string FindingsJson { get; private set; } = "[]";

    public decimal BiasRiskScore { get; private set; }

    // TIMESTAMPS
    public DateTimeOffset CreatedAt { get; private set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    // ERROR
    public string? Error { get; private set; }

    private AuditJob()
    {
    }

    public static AuditJob Create(
        Guid userId,
        string objectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? modelDecision,
        string? cohort,
        string? notes)
    {
        return new AuditJob
        {
            UserId = userId,
            ObjectKey = objectKey,
            OriginalFileName = Path.GetFileName(originalFileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ModelDecision = Normalize(modelDecision),
            Cohort = Normalize(cohort),
            Notes = Normalize(notes)
        };
    }

    public void MarkProcessing()
    {
        Status = AuditStatus.Processing;
        StartedAt = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void Complete(
        IReadOnlyCollection<NudeNetDetection> detections,
        AuditFindingSet findingSet)
    {
        Status = AuditStatus.Completed;

        CompletedAt = DateTimeOffset.UtcNow;

        NudeNetJson = JsonSerializer.Serialize(
            detections,
            JsonDefaults.Options);

        FindingsJson = JsonSerializer.Serialize(
            findingSet.Findings,
            JsonDefaults.Options);

        BiasRiskScore = findingSet.Score;

        Error = null;
    }

    public void Fail(string message)
    {
        Status = AuditStatus.Failed;

        CompletedAt = DateTimeOffset.UtcNow;

        Error = message;
    }

    public IReadOnlyCollection<NudeNetDetection> GetDetections()
    {
        return JsonSerializer.Deserialize<
                   IReadOnlyCollection<NudeNetDetection>>(
                   NudeNetJson,
                   JsonDefaults.Options)
               ?? [];
    }

    public IReadOnlyCollection<AuditFinding> GetFindings()
    {
        return JsonSerializer.Deserialize<
                   IReadOnlyCollection<AuditFinding>>(
                   FindingsJson,
                   JsonDefaults.Options)
               ?? [];
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

public enum AuditStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
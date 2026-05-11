namespace BiasAudit.Api.Options;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public long MaxUploadBytes { get; init; } = 10 * 1024 * 1024;
    public string[] AllowedContentTypes { get; init; } = ["image/jpeg", "image/png", "image/webp"];
}

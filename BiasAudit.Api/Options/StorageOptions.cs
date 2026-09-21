namespace BiasAudit.Api.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string BucketName { get; init; } = "bias-audits";
    public string AccessKey { get; init; } = "minioadmin";
    public string SecretKey { get; init; } = "minioadmin";
    public string Region { get; init; } = "us-east-1";
    public string? ServiceUrl { get; init; } = "http://localhost:9000";
    public bool ForcePathStyle { get; init; } = true;
    public bool UseLocalFileStorage { get; init; }
}

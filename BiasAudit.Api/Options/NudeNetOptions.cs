namespace BiasAudit.Api.Options;

public sealed class NudeNetOptions
{
    public const string SectionName = "NudeNet";

    public string BaseUrl { get; init; } = "http://localhost:8081";
    public string DetectPath { get; init; } = "/detect";
    public bool UseMockWhenUnavailable { get; init; } = true;
}

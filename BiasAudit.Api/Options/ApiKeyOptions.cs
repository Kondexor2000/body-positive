namespace BiasAudit.Api.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; init; } = "X-Api-Key";
    public string Value { get; init; } = "dev-api-key-change-me";
}

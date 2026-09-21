namespace BiasAudit.Api.Contracts;

public sealed record UpdateSettingsRequest(
    string? Email,
    string? Password);
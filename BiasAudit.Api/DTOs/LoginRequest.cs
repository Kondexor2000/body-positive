namespace BiasAudit.Api.Contracts;

public sealed record LoginRequest(
    string Username,
    string Password);
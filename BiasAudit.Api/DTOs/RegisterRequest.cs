namespace BiasAudit.Api.Contracts;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password);
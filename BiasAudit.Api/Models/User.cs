namespace BiasAudit.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    // Optional contact detail reserved for a future account-recovery flow.
    // It is not used to sign in or to give publication consent.
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

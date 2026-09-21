namespace BiasAudit.Api.Models;

public sealed class RevokedToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JwtId { get; set; } = string.Empty;

    public DateTime RevokedAtUtc { get; set; } = DateTime.UtcNow;
}

namespace BiasAudit.Api.Services;

public interface IJwtService
{
    string GenerateToken(Guid userId, string username);
}
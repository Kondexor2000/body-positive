using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(AuditDbContext db) : ControllerBase
{
    /// <summary>
    /// Provides a safe, minimal directory for the tagging control. E-mail
    /// addresses and password-related fields are never returned.
    /// </summary>
    [HttpGet("taggable")]
    public async Task<ActionResult<IEnumerable<UserTagOptionResponse>>> TaggableUsers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        var normalizedSearch = search?.Trim();

        var users = db.Users
            .AsNoTracking()
            .Where(x => x.Id != currentUserId);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            users = users.Where(x => EF.Functions.ILike(x.Username, $"%{normalizedSearch}%"));
        }

        var options = await users
            .OrderBy(x => x.Username)
            .Take(100)
            .Select(x => new UserTagOptionResponse(x.Id, x.Username))
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Missing JWT subject.");
        return userId;
    }
}

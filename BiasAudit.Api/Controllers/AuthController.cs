using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Contracts;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuditDbContext db,
    IJwtService jwtService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(x => x.Username == request.Username, ct);

        if (exists)
            return BadRequest(new { message = "Username exists" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return StatusCode(201, new { message = "User created" });
    }
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Username == request.Username, ct);

        if (user is null)
            return Unauthorized();

        var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!valid)
            return Unauthorized();

        var token = jwtService.GenerateToken(user.Id, user.Username);

        // 🔥 WAŻNE: zawsze accessToken
        return Ok(new AuthResponse(token));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = GetUserIdClaim();

        if (!Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == guid, ct);

        if (user is null)
            return NotFound();

        return Ok(new
        {
            user.Username,
            user.Email
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);

        if (jti is null)
            return Unauthorized();

        db.RevokedTokens.Add(new RevokedToken { JwtId = jti });
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Logged out" });
    }

    // ---------------- SETTINGS ----------------
    [Authorize]
    [HttpPut("settings")]
    public async Task<IActionResult> Settings(
        UpdateSettingsRequest request,
        CancellationToken ct)
    {
        var userIdValue = GetUserIdClaim();

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Updated" });
    }

    // ---------------- DELETE ----------------
    [Authorize]
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var userIdValue = GetUserIdClaim();

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            return NotFound();
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Account deleted" });
    }

    private string? GetUserIdClaim() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
}

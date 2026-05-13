using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Contracts;
using BiasAudit.Api.Controllers;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static BCrypt.Net.BCrypt;

namespace BiasAudit.Api.Tests.Controllers;

public sealed class AuthControllerTests : IDisposable
{
    private readonly AuditDbContext _db;

    private readonly Mock<IJwtService> _mockJwtService = new();

    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase($"auth-{Guid.NewGuid():N}")
            .Options;

        _db = new AuditDbContext(dbOptions);

        _controller = new AuthController(
            _db,
            _mockJwtService.Object);
    }

    [Fact]
    public async Task Register_ValidRequest_CreatesUser()
    {
        var request = new RegisterRequest(
            "testuser",
            "test@example.com",
            "password123");

        var result = await _controller.Register(
            request,
            CancellationToken.None);

        var statusCodeResult =
            Assert.IsType<ObjectResult>(result);

        Assert.Equal(201, statusCodeResult.StatusCode);

        var user = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Username == "testuser");

        Assert.NotNull(user);

        Assert.Equal(
            "test@example.com",
            user.Email);

        Assert.True(
            Verify(
                "password123",
                user.PasswordHash));
    }

    [Fact]
    public async Task Register_ExistingUsername_ReturnsBadRequest()
    {
        var existingUser = new User
        {
            Username = "existinguser",
            Email = "existing@example.com",
            PasswordHash = HashPassword("password")
        };

        _db.Users.Add(existingUser);

        await _db.SaveChangesAsync();

        var request = new RegisterRequest(
            "existinguser",
            "new@example.com",
            "password123");

        var result = await _controller.Register(
            request,
            CancellationToken.None);

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        var user = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = HashPassword("password123")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        _mockJwtService
            .Setup(x => x.GenerateToken(user.Id, "testuser"))
            .Returns("valid-jwt-token");

        var request = new LoginRequest(
            "testuser",
            "password123");

        var result = await _controller.Login(
            request,
            CancellationToken.None);

        var okResult =
            Assert.IsType<OkObjectResult>(result.Result);

        var response =
            Assert.IsType<AuthResponse>(okResult.Value);

        Assert.Equal(
            "valid-jwt-token",
            response.AccessToken);
    }

    [Fact]
    public async Task Login_NonExistingUser_ReturnsUnauthorized()
    {
        var request = new LoginRequest(
            "nonexistent",
            "password123");

        var result = await _controller.Login(
            request,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var user = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = HashPassword("correctpassword")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        var request = new LoginRequest(
            "testuser",
            "wrongpassword");

        var result = await _controller.Login(
            request,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Logout_ValidJwt_RevokesToken()
    {
        var testJti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Jti,
                testJti)
        };

        var identity = new ClaimsIdentity(claims);

        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var result = await _controller.Logout(
            CancellationToken.None);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        var revokedToken = await _db.RevokedTokens
            .FirstOrDefaultAsync(x =>
                x.JwtId == testJti);

        Assert.NotNull(revokedToken);
    }

    [Fact]
    public async Task Me_AuthorizedUser_ReturnsUserInfo()
    {
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = HashPassword("password")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString())
        };

        var identity = new ClaimsIdentity(claims);

        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var result = await _controller.Me(
            CancellationToken.None);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Me_NonExistingUser_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString())
        };

        var identity = new ClaimsIdentity(claims);

        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

        var result = await _controller.Me(
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Settings_UpdateEmail_Success()
    {
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "old@example.com",
            PasswordHash = HashPassword("password")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        SetAuthenticatedUser(userId);

        var request = new UpdateSettingsRequest(
            "new@example.com",
            null);

        var result = await _controller.Settings(
            request,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var updatedUser = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId);

        Assert.Equal(
            "new@example.com",
            updatedUser?.Email);
    }

    [Fact]
    public async Task Settings_UpdatePassword_Success()
    {
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = HashPassword("oldpassword")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        SetAuthenticatedUser(userId);

        var request = new UpdateSettingsRequest(
            null,
            "newpassword");

        var result = await _controller.Settings(
            request,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var updatedUser = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId);

        Assert.NotNull(updatedUser);

        Assert.True(
            Verify(
                "newpassword",
                updatedUser.PasswordHash));
    }

    [Fact]
    public async Task Delete_AuthorizedUser_DeletesAccount()
    {
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = HashPassword("password")
        };

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        SetAuthenticatedUser(userId);

        var result = await _controller.Delete(
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var deletedUser = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId);

        Assert.Null(deletedUser);
    }

    private void SetAuthenticatedUser(Guid userId)
    {
        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString())
        };

        var identity = new ClaimsIdentity(claims);

        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
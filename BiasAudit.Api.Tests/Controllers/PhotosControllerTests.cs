using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Controllers;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Options;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace BiasAudit.Api.Tests.Controllers;

public sealed class PhotosControllerTests : IDisposable
{
    private readonly AuditDbContext _db;
    private readonly Mock<IObjectStorage> _storage = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _taggedId = Guid.NewGuid();

    public PhotosControllerTests()
    {
        _db = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase($"photos-{Guid.NewGuid():N}").Options);

        _db.Users.AddRange(
            new User { Id = _ownerId, Username = "owner", Email = "owner@example.com", PasswordHash = "hash" },
            new User { Id = _taggedId, Username = "tagged", Email = "tagged@example.com", PasswordHash = "hash" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Create_TaggedPerson_KeepsPhotoPendingUntilApproval()
    {
        var controller = CreateController(_ownerId);
        var file = ImageFile();
        _storage.Setup(x => x.UploadAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("private/photo-key");

        var result = await controller.Create(file, ["tagged"], CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<CreatePhotoResponse>(created.Value);
        Assert.Equal("PendingConsent", response.PublicationStatus);
        Assert.Equal(1, response.PendingConsents);
        Assert.Equal(PhotoPublicationStatus.PendingConsent, (await _db.PhotoPosts.SingleAsync()).PublicationStatus);
    }

    [Fact]
    public async Task Approve_LastOutstandingConsent_PublishesPhoto()
    {
        var photo = CreatePendingPhoto();
        var controller = CreateController(_taggedId);

        var result = await controller.Approve(photo.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PhotoResponse>(ok.Value);
        Assert.Equal("Published", response.PublicationStatus);
        Assert.Equal(PhotoPublicationStatus.Published, (await _db.PhotoPosts.SingleAsync()).PublicationStatus);
    }

    [Fact]
    public async Task Decline_StopsPublication()
    {
        var photo = CreatePendingPhoto();
        var controller = CreateController(_taggedId);

        var result = await controller.Decline(photo.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Rejected", Assert.IsType<PhotoResponse>(ok.Value).PublicationStatus);
        Assert.Equal(PhotoPublicationStatus.Rejected, (await _db.PhotoPosts.SingleAsync()).PublicationStatus);
    }

    private PhotoPost CreatePendingPhoto()
    {
        var photo = PhotoPost.Create(_ownerId, "private/key", "photo.jpg", "image/jpeg", 3, requiresConsent: true);
        photo.TagConsents.Add(PhotoTagConsent.Create(_taggedId));
        _db.PhotoPosts.Add(photo);
        _db.SaveChanges();
        return photo;
    }

    private PhotosController CreateController(Guid userId)
    {
        var controller = new PhotosController(_db, _storage.Object, OptionsFactory.Create(new AuditOptions()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]))
            }
        };
        return controller;
    }

    private static IFormFile ImageFile() => new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "photo.jpg")
    {
        Headers = new HeaderDictionary(),
        ContentType = "image/jpeg"
    };

    public void Dispose() => _db.Dispose();
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Options;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BiasAudit.Api.Controllers;

/// <summary>
/// Consent-gated photo publication. A pending image is visible only to its
/// uploader and the people asked for consent; it is not part of the public feed.
/// </summary>
[ApiController]
[Authorize]
[Route("api/photos")]
public sealed class PhotosController(
    AuditDbContext db,
    IObjectStorage storage,
    IOptions<AuditOptions> auditOptions) : ControllerBase
{
    private readonly AuditOptions _auditOptions = auditOptions.Value;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CreatePhotoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatePhotoResponse>> Create(
        IFormFile file,
        [FromForm] List<string>? taggedUsernames,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest(new ProblemDetailsResponse("Nie można opublikować pustego pliku."));

        if (file.Length > _auditOptions.MaxUploadBytes)
            return BadRequest(new ProblemDetailsResponse($"Plik przekracza limit {_auditOptions.MaxUploadBytes} bajtów."));

        if (!_auditOptions.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new ProblemDetailsResponse("Dozwolone są wyłącznie obrazy JPEG, PNG i WebP."));

        var ownerId = GetUserId();
        var usernames = (taggedUsernames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var taggedUsers = await db.Users
            .Where(x => usernames.Contains(x.Username))
            .ToListAsync(cancellationToken);

        if (taggedUsers.Count != usernames.Length)
        {
            var found = taggedUsers.Select(x => x.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = usernames.Where(x => !found.Contains(x));
            return BadRequest(new ProblemDetailsResponse(
                $"Nie znaleziono użytkowników: {string.Join(", ", missing)}."));
        }

        // The uploader's action is their own consent, so only other people need a request.
        var peopleRequiringConsent = taggedUsers.Where(x => x.Id != ownerId).ToArray();

        await using var stream = file.OpenReadStream();
        var objectKey = await storage.UploadAsync(stream, file.FileName, file.ContentType, cancellationToken);

        var photo = PhotoPost.Create(
            ownerId, objectKey, file.FileName, file.ContentType, file.Length,
            peopleRequiringConsent.Length > 0);

        foreach (var taggedUser in peopleRequiringConsent)
            photo.TagConsents.Add(PhotoTagConsent.Create(taggedUser.Id));

        db.PhotoPosts.Add(photo);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = photo.Id },
            new CreatePhotoResponse(photo.Id, photo.PublicationStatus.ToString(), peopleRequiringConsent.Length));
    }

    [HttpGet("consent-requests")]
    public async Task<ActionResult<IEnumerable<PhotoConsentResponse>>> ConsentRequests(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var requests = await db.PhotoTagConsents
            .AsNoTracking()
            .Where(x => x.TaggedUserId == userId && x.Status == TagConsentStatus.Pending)
            .Include(x => x.PhotoPost).ThenInclude(x => x.Owner)
            .OrderByDescending(x => x.PhotoPost.CreatedAt)
            .Select(x => new PhotoConsentResponse(
                x.PhotoPostId,
                x.PhotoPost.PublicationStatus.ToString(),
                x.PhotoPost.OriginalFileName,
                x.PhotoPost.Owner.Username,
                x.PhotoPost.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<PhotoResponse>>> MyPhotos(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var photos = await PhotoQuery()
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(photos.Select(PhotoResponse.From));
    }

    [HttpPost("{id:guid}/consents/approve")]
    public Task<ActionResult<PhotoResponse>> Approve(Guid id, CancellationToken cancellationToken) =>
        RespondToConsent(id, approve: true, cancellationToken);

    [HttpPost("{id:guid}/consents/decline")]
    public Task<ActionResult<PhotoResponse>> Decline(Guid id, CancellationToken cancellationToken) =>
        RespondToConsent(id, approve: false, cancellationToken);

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PhotoResponse>>> PublicPhotos(CancellationToken cancellationToken)
    {
        var photos = await PhotoQuery()
            .AsNoTracking()
            .Where(x => x.PublicationStatus == PhotoPublicationStatus.Published)
            .OrderByDescending(x => x.PublishedAt)
            .ToListAsync(cancellationToken);

        return Ok(photos.Select(PhotoResponse.From));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PhotoResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetOptionalUserId();
        var photo = await PhotoQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (photo is null || !CanView(photo, userId))
            return NotFound(new ProblemDetailsResponse("Nie znaleziono zdjęcia."));

        return Ok(PhotoResponse.From(photo));
    }

    [HttpGet("{id:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> Content(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetOptionalUserId();
        var photo = await PhotoQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (photo is null || !CanView(photo, userId))
            return NotFound(new ProblemDetailsResponse("Nie znaleziono zdjęcia."));

        var stream = await storage.DownloadAsync(photo.ObjectKey, cancellationToken);
        return File(stream, photo.ContentType, enableRangeProcessing: true);
    }

    private async Task<ActionResult<PhotoResponse>> RespondToConsent(
        Guid photoId,
        bool approve,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var photo = await PhotoQuery().FirstOrDefaultAsync(x => x.Id == photoId, cancellationToken);
        if (photo is null)
            return NotFound(new ProblemDetailsResponse("Nie znaleziono zdjęcia."));

        var consent = photo.TagConsents.SingleOrDefault(x => x.TaggedUserId == userId);
        if (consent is null)
            return Forbid();

        if (photo.PublicationStatus != PhotoPublicationStatus.PendingConsent)
            return BadRequest(new ProblemDetailsResponse("Na to zdjęcie nie można już odpowiedzieć."));

        if (approve)
            consent.Approve();
        else
            consent.Decline();

        if (!approve)
            photo.RejectPublication();
        else if (photo.TagConsents.All(x => x.Status == TagConsentStatus.Approved))
            photo.Publish();

        await db.SaveChangesAsync(cancellationToken);
        return Ok(PhotoResponse.From(photo));
    }

    private IQueryable<PhotoPost> PhotoQuery() => db.PhotoPosts
        .Include(x => x.Owner)
        .Include(x => x.TagConsents).ThenInclude(x => x.TaggedUser);

    private static bool CanView(PhotoPost photo, Guid? userId) =>
        photo.PublicationStatus == PhotoPublicationStatus.Published ||
        (userId.HasValue && (photo.OwnerId == userId ||
            photo.TagConsents.Any(x => x.TaggedUserId == userId)));

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Missing JWT subject.");
        return userId;
    }

    private Guid? GetOptionalUserId() =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out var userId)
            ? userId
            : null;
}

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

[ApiController]
[Authorize]
[Route("api/audits")]
public sealed class AuditsController(
    AuditDbContext db,
    IObjectStorage storage,
    IBackgroundAuditQueue queue,
    IReportRenderer renderer,
    IOptions<AuditOptions> auditOptions) : ControllerBase
{
    private readonly AuditOptions _auditOptions = auditOptions.Value;

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException("Missing JWT subject.");
        }

        return Guid.Parse(value);
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType<UploadAuditResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UploadAuditResponse>> Upload(
        IFormFile file,
        [FromForm] string? modelDecision,
        [FromForm] string? cohort,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(
                new ProblemDetailsResponse(
                    "Pusty plik nie moze zostac poddany audytowi."));
        }

        if (file.Length > _auditOptions.MaxUploadBytes)
        {
            return BadRequest(
                new ProblemDetailsResponse(
                    $"Plik przekracza limit {_auditOptions.MaxUploadBytes} bajtow."));
        }

        if (!_auditOptions.AllowedContentTypes.Contains(
                file.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(
                new ProblemDetailsResponse(
                    "Dozwolone sa wylacznie obrazy JPEG, PNG i WebP."));
        }

        var userId = GetUserId();

        await using var stream = file.OpenReadStream();

        var objectKey = await storage.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        var job = AuditJob.Create(
            userId,
            objectKey,
            file.FileName,
            file.ContentType,
            file.Length,
            modelDecision,
            cohort,
            notes);

        db.AuditJobs.Add(job);

        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(job.Id, cancellationToken);

        return AcceptedAtAction(
            nameof(Get),
            new { id = job.Id },
            new UploadAuditResponse(
                job.Id,
                job.Status.ToString()));
    }

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<AuditStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<AuditStatusResponse>>> MyAudits(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var jobs = await db.AuditJobs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(jobs.Select(AuditStatusResponse.From));
    }

    [HttpGet("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<AuditStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuditStatusResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var job = await db.AuditJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     x.UserId == userId,
                cancellationToken);

        if (job is null)
        {
            return NotFound(
                new ProblemDetailsResponse(
                    "Nie znaleziono audytu."));
        }

        return Ok(AuditStatusResponse.From(job));
    }

    [HttpGet("{id:guid}/report")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Report(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var job = await db.AuditJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     x.UserId == userId,
                cancellationToken);

        if (job is null)
        {
            return NotFound(
                new ProblemDetailsResponse(
                    "Nie znaleziono audytu."));
        }

        if (job.Status != AuditStatus.Completed)
        {
            return BadRequest(
                new ProblemDetailsResponse(
                    "Raport bedzie dostepny po zakonczeniu audytu."));
        }

        var html = renderer.Render(job);

        return Content(
            html,
            "text/html; charset=utf-8");
    }
}
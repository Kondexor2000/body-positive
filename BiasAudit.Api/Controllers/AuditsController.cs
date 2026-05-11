using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Options;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BiasAudit.Api.Controllers;

[ApiController]
[Route("api/audits")]
public sealed class AuditsController(
    AuditDbContext db,
    IObjectStorage storage,
    IBackgroundAuditQueue queue,
    IReportRenderer renderer,
    IOptions<AuditOptions> auditOptions) : ControllerBase
{
    private readonly AuditOptions _auditOptions = auditOptions.Value;

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType<UploadAuditResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadAuditResponse>> Upload(
        IFormFile file,
        [FromForm] string? modelDecision,
        [FromForm] string? cohort,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new ProblemDetailsResponse("Pusty plik nie moze zostac poddany audytowi."));
        }

        if (file.Length > _auditOptions.MaxUploadBytes)
        {
            return BadRequest(new ProblemDetailsResponse($"Plik przekracza limit {_auditOptions.MaxUploadBytes} bajtow."));
        }

        if (!_auditOptions.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetailsResponse("Dozwolone sa wylacznie obrazy JPEG, PNG i WebP."));
        }

        await using var stream = file.OpenReadStream();
        var objectKey = await storage.UploadAsync(stream, file.FileName, file.ContentType, cancellationToken);

        var job = AuditJob.Create(
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

        return AcceptedAtAction(nameof(Get), new { id = job.Id }, new UploadAuditResponse(job.Id, job.Status.ToString()));
    }

    [HttpGet("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<AuditStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditStatusResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.AuditJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null)
        {
            return NotFound(new ProblemDetailsResponse("Nie znaleziono audytu."));
        }

        return Ok(AuditStatusResponse.From(job));
    }

    [HttpGet("{id:guid}/report")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetailsResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.AuditJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null)
        {
            return NotFound(new ProblemDetailsResponse("Nie znaleziono audytu."));
        }

        if (job.Status != AuditStatus.Completed)
        {
            return BadRequest(new ProblemDetailsResponse("Raport bedzie dostepny po zakonczeniu audytu."));
        }

        var html = renderer.Render(job);
        return Content(html, "text/html; charset=utf-8");
    }
}

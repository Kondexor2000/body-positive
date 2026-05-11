using BiasAudit.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Services;

public sealed class AuditWorker(
    IBackgroundAuditQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var auditId = await queue.DequeueAsync(stoppingToken);
            await ProcessAsync(auditId, stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid auditId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var nudeNet = scope.ServiceProvider.GetRequiredService<INudeNetClient>();

        var job = await db.AuditJobs.FirstOrDefaultAsync(x => x.Id == auditId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Audit job {AuditId} was queued but no database record exists.", auditId);
            return;
        }

        try
        {
            job.MarkProcessing();
            await db.SaveChangesAsync(cancellationToken);

            await using var image = await storage.DownloadAsync(job.ObjectKey, cancellationToken);
            var detections = await nudeNet.DetectAsync(image, job.OriginalFileName, job.ContentType, cancellationToken);
            var findings = AuditScoring.Score(job, detections);

            job.Complete(detections, findings);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit job {AuditId} failed.", auditId);
            job.Fail(ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}

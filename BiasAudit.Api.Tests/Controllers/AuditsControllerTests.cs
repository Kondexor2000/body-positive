using BiasAudit.Api.Controllers;
using BiasAudit.Api.Data;
using BiasAudit.Api.Models;
using BiasAudit.Api.Options;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace BiasAudit.Api.Tests.Controllers;

public sealed class AuditsControllerTests : IDisposable
{
    private readonly AuditDbContext _db;
    private readonly Mock<IObjectStorage> _mockStorage = new();
    private readonly Mock<IBackgroundAuditQueue> _mockQueue = new();
    private readonly Mock<IReportRenderer> _mockRenderer = new();
    private readonly AuditOptions _auditOptions = new();
    private readonly AuditsController _controller;

    public AuditsControllerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase($"audits-{Guid.NewGuid():N}")
            .Options;

        _db = new AuditDbContext(dbOptions);
        _controller = new AuditsController(
            _db,
            _mockStorage.Object,
            _mockQueue.Object,
            _mockRenderer.Object,
            OptionsFactory.Create(_auditOptions));
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0);

        var result = await _controller.Upload(fileMock.Object, null, null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ProblemDetailsResponse>(badRequest.Value);
        Assert.Contains("Pusty plik", response.Message);
    }

    [Fact]
    public async Task Upload_FileTooLarge_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(_auditOptions.MaxUploadBytes + 1);

        var result = await _controller.Upload(fileMock.Object, null, null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ProblemDetailsResponse>(badRequest.Value);
        Assert.Contains("limit", response.Message);
    }

    [Fact]
    public async Task Upload_InvalidContentType_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1000);
        fileMock.Setup(f => f.ContentType).Returns("text/plain");

        var result = await _controller.Upload(fileMock.Object, null, null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ProblemDetailsResponse>(badRequest.Value);
        Assert.Contains("obrazy", response.Message);
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsAccepted()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1000);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));

        _mockStorage
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "test.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("object-key");

        var result = await _controller.Upload(fileMock.Object, "decision", "cohort", "notes", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedAtActionResult>(result.Result);
        var response = Assert.IsType<UploadAuditResponse>(accepted.Value);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Queued", response.Status);
        Assert.Equal(1, await _db.AuditJobs.CountAsync());
        _mockQueue.Verify(q => q.EnqueueAsync(response.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_ExistingJob_ReturnsOk()
    {
        var job = AuditJob.Create("key", "file.jpg", "image/jpeg", 1000, null, null, null);
        _db.AuditJobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _controller.Get(job.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditStatusResponse>(okResult.Value);
        Assert.Equal(job.Id, response.Id);
    }

    [Fact]
    public async Task Get_NonExistingJob_ReturnsNotFound()
    {
        var result = await _controller.Get(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ProblemDetailsResponse>(notFound.Value);
        Assert.Contains("Nie znaleziono", response.Message);
    }

    [Fact]
    public async Task Report_CompletedJob_ReturnsHtml()
    {
        var job = AuditJob.Create("key", "file.jpg", "image/jpeg", 1000, null, null, null);
        job.Complete([], new AuditFindingSet(0.1m, []));
        _db.AuditJobs.Add(job);
        await _db.SaveChangesAsync();

        _mockRenderer.Setup(r => r.Render(It.Is<AuditJob>(x => x.Id == job.Id))).Returns("<html>Test Report</html>");

        var result = await _controller.Report(job.Id, CancellationToken.None);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html; charset=utf-8", contentResult.ContentType);
        Assert.Equal("<html>Test Report</html>", contentResult.Content);
    }

    [Fact]
    public async Task Report_IncompleteJob_ReturnsBadRequest()
    {
        var job = AuditJob.Create("key", "file.jpg", "image/jpeg", 1000, null, null, null);
        job.MarkProcessing();
        _db.AuditJobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _controller.Report(job.Id, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ProblemDetailsResponse>(badRequest.Value);
        Assert.Contains("zakonczeniu", response.Message);
    }

    public void Dispose() => _db.Dispose();
}

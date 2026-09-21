namespace BiasAudit.Api.Services;

/// <summary>
/// Development storage that keeps uploads on the local machine when MinIO/S3 is
/// not running. It implements the same private object-storage contract as S3.
/// </summary>
public sealed class LocalObjectStorage(IWebHostEnvironment environment) : IObjectStorage
{
    private readonly string _root = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");

    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        var objectKey = Path.Combine(
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            $"{Guid.NewGuid():N}{extension}")
            .Replace('\\', '/');
        var fullPath = ResolvePath(objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await stream.CopyToAsync(destination, cancellationToken);
        return objectKey;
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken)
    {
        Stream stream = new FileStream(
            ResolvePath(objectKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    private string ResolvePath(string objectKey)
    {
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage object key.");
        return path;
    }
}

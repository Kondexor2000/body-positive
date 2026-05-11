using Amazon.S3;
using Amazon.S3.Model;
using BiasAudit.Api.Options;
using Microsoft.Extensions.Options;

namespace BiasAudit.Api.Services;

public interface IObjectStorage
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class S3ObjectStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IObjectStorage
{
    private readonly StorageOptions _options = options.Value;

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        var extension = Path.GetExtension(fileName);
        var objectKey = $"uploads/{DateTimeOffset.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType
        }, cancellationToken);

        return objectKey;
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await s3.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);
        var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var buckets = await s3.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets.Any(x => x.BucketName == _options.BucketName))
        {
            return;
        }

        await s3.PutBucketAsync(new PutBucketRequest
        {
            BucketName = _options.BucketName
        }, cancellationToken);
    }
}

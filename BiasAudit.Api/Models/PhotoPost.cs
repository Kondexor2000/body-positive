namespace BiasAudit.Api.Models;

/// <summary>
/// A photo submitted for publication. The object in storage is never exposed by
/// this API until all people tagged in the photo give their consent.
/// </summary>
public sealed class PhotoPost
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid OwnerId { get; private set; }
    public User Owner { get; private set; } = default!;

    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    public PhotoPublicationStatus PublicationStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; private set; }

    public ICollection<PhotoTagConsent> TagConsents { get; private set; } = new List<PhotoTagConsent>();

    private PhotoPost()
    {
    }

    public static PhotoPost Create(
        Guid ownerId,
        string objectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        bool requiresConsent) =>
        new()
        {
            OwnerId = ownerId,
            ObjectKey = objectKey,
            OriginalFileName = Path.GetFileName(originalFileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            PublicationStatus = requiresConsent
                ? PhotoPublicationStatus.PendingConsent
                : PhotoPublicationStatus.Published,
            PublishedAt = requiresConsent ? null : DateTimeOffset.UtcNow
        };

    public void Publish()
    {
        PublicationStatus = PhotoPublicationStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
    }

    public void RejectPublication()
    {
        PublicationStatus = PhotoPublicationStatus.Rejected;
        PublishedAt = null;
    }
}

public sealed class PhotoTagConsent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PhotoPostId { get; private set; }
    public PhotoPost PhotoPost { get; private set; } = default!;
    public Guid TaggedUserId { get; private set; }
    public User TaggedUser { get; private set; } = default!;
    public TagConsentStatus Status { get; private set; } = TagConsentStatus.Pending;
    public DateTimeOffset? RespondedAt { get; private set; }

    private PhotoTagConsent()
    {
    }

    public static PhotoTagConsent Create(Guid taggedUserId) => new() { TaggedUserId = taggedUserId };

    public void Approve()
    {
        Status = TagConsentStatus.Approved;
        RespondedAt = DateTimeOffset.UtcNow;
    }

    public void Decline()
    {
        Status = TagConsentStatus.Declined;
        RespondedAt = DateTimeOffset.UtcNow;
    }
}

public enum PhotoPublicationStatus
{
    PendingConsent,
    Published,
    Rejected
}

public enum TagConsentStatus
{
    Pending,
    Approved,
    Declined
}

namespace BiasAudit.Api.Models;

public sealed record CreatePhotoResponse(Guid Id, string PublicationStatus, int PendingConsents);

public sealed record PhotoConsentResponse(
    Guid PhotoId,
    string PublicationStatus,
    string OriginalFileName,
    string OwnerUsername,
    DateTimeOffset CreatedAt);

public sealed record PhotoResponse(
    Guid Id,
    string PublicationStatus,
    string OriginalFileName,
    string OwnerUsername,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyCollection<PhotoTagResponse> TaggedUsers)
{
    public static PhotoResponse From(PhotoPost photo) => new(
        photo.Id,
        photo.PublicationStatus.ToString(),
        photo.OriginalFileName,
        photo.Owner.Username,
        photo.CreatedAt,
        photo.PublishedAt,
        photo.TagConsents
            .OrderBy(x => x.TaggedUser.Username)
            .Select(x => new PhotoTagResponse(x.TaggedUser.Username, x.Status.ToString()))
            .ToArray());
}

public sealed record PhotoTagResponse(string Username, string ConsentStatus);

public sealed record UserTagOptionResponse(Guid Id, string Username);

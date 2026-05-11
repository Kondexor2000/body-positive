using System.Net.Http.Json;
using BiasAudit.Api.Models;
using BiasAudit.Api.Options;
using Microsoft.Extensions.Options;

namespace BiasAudit.Api.Services;

public interface INudeNetClient
{
    Task<IReadOnlyCollection<NudeNetDetection>> DetectAsync(Stream image, string fileName, string contentType, CancellationToken cancellationToken);
}

public sealed class NudeNetHttpClient(
    HttpClient httpClient,
    IOptions<NudeNetOptions> options,
    ILogger<NudeNetHttpClient> logger) : INudeNetClient
{
    private readonly NudeNetOptions _options = options.Value;

    public async Task<IReadOnlyCollection<NudeNetDetection>> DetectAsync(
        Stream image,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(image)
            {
                Headers = { ContentType = new(contentType) }
            }, "file", fileName);

            var endpoint = new Uri(new Uri(_options.BaseUrl), _options.DetectPath);
            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<NudeNetDetection>>(
                JsonDefaults.Options,
                cancellationToken);

            return result ?? [];
        }
        catch (Exception ex) when (_options.UseMockWhenUnavailable)
        {
            logger.LogWarning(ex, "NudeNet endpoint is unavailable. Returning an empty mock result.");
            return [];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class FigmaApi : IDisposable
{
    private const string BaseUri = "https://api.figma.com/v1";

    // Static singleton HttpClient — avoids socket exhaustion from repeated Dispose/recreate
    private static HttpClient _sharedClient;
    private static readonly object _clientLock = new object();

    private static HttpClient GetOrCreateClient(string token)
    {
        lock (_clientLock)
        {
            if (_sharedClient != null)
                return _sharedClient;

            _sharedClient = new HttpClient();
            _sharedClient.DefaultRequestHeaders.Add("X-FIGMA-TOKEN", token);
            _sharedClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
            _sharedClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
            };
            return _sharedClient;
        }
    }

    private readonly HttpClient _httpClient;

    public FigmaApi(string personalAccessToken)
    {
        _httpClient = GetOrCreateClient(personalAccessToken);
    }

    /// <summary>
    /// Downloads all images present in image fills in a document.
    /// Uses parallel Task.WhenAll to download all images concurrently.
    /// </summary>
    /// <see href="https://www.figma.com/developers/api#get-image-fills-endpoint"/>
    public async Task<Dictionary<string, byte[]>> GetImageFillsAsync(
        ImageFillsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrEmpty(request.fileId))
            throw new ArgumentException("File ID cannot be empty.");

        var url = GetImageFillsRequestUrl(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

        using var httpResponse = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var response = JsonHelper.Deserialize<ImageFillsResponse>(json);

        if (response == null || response.metadata == null)
            return null;

        // Filter to only the requested imageRefs that have valid URLs
        var filteredImages = response.metadata.images
            .Where(kvp => request.imageRefs == null || request.imageRefs.Contains(kvp.Key))
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .ToList();

        // Download all images in parallel
        var downloadTasks = filteredImages.Select(async kvp =>
        {
            var imageData = await GetBytesAsync(kvp.Value, "application/octet-stream", cancellationToken)
                .ConfigureAwait(false);
            if (imageData == null)
                throw new Exception($"Image '{kvp.Key}' data could not be retrieved from: {kvp.Value}");
            return (imageRef: kvp.Key, data: imageData);
        });

        var results = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        var images = new Dictionary<string, byte[]>();
        foreach (var (imageRef, data) in results)
            images[imageRef] = data;

        // Add null entries for refs with empty URLs (Figma marks these as unrenderable)
        foreach (var kvp in response.metadata.images)
        {
            if ((request.imageRefs == null || request.imageRefs.Contains(kvp.Key))
                && string.IsNullOrEmpty(kvp.Value)
                && !images.ContainsKey(kvp.Key))
            {
                images[kvp.Key] = null;
            }
        }

        return images;
    }

    /// <summary>
    /// Renders images from a file.
    /// Uses parallel Task.WhenAll to download all rendered images concurrently.
    /// </summary>
    public async Task<Dictionary<string, byte[]>> GetImageAsync(
        ImageRequest imageRequest,
        CancellationToken cancellationToken = default
    )
    {
        if (imageRequest == null)
            throw new ArgumentNullException(nameof(imageRequest));

        if (string.IsNullOrEmpty(imageRequest.fileId))
            throw new ArgumentException("File ID cannot be empty.");

        if (imageRequest.ids == null || imageRequest.ids.Length == 0)
            throw new ArgumentException("Image ids array must have at least one item.");

        // Get image download URLs
        var url = GetImageRequestUrl(imageRequest);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var imageResponse = JsonHelper.Deserialize<ImageResponse>(json);
        if (imageResponse == null)
            return null;

        // Download all rendered images in parallel
        var validImages = imageResponse.images
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .ToList();

        var downloadTasks = validImages.Select(async kvp =>
        {
            var imageData = await GetBytesAsync(kvp.Value, "application/octet-stream", cancellationToken);
            if (imageData == null)
                throw new Exception($"Image '{kvp.Key}' data could not be retrieved from: {kvp.Value}");
            return (imageId: kvp.Key, data: imageData);
        });

        var results = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        var images = new Dictionary<string, byte[]>();
        foreach (var (imageId, data) in results)
            images[imageId] = data;

        // Add null entries for images that failed to render on Figma's side
        foreach (var kvp in imageResponse.images)
        {
            if (string.IsNullOrEmpty(kvp.Value) && !images.ContainsKey(kvp.Key))
                images[kvp.Key] = null;
        }

        return images;
    }

    public async Task<byte[]> GetThumbnailImageAsync(
        string thumbnailUrl,
        CancellationToken cancellationToken = default
    )
    {
        return await GetBytesAsync(thumbnailUrl, "image/png", cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetImageFillsRequestUrl(ImageFillsRequest request)
    {
        return $"{BaseUri}/files/{request.fileId}/images";
    }

    private static string GetImageRequestUrl(ImageRequest request)
    {
        var url = $"{BaseUri}/images/{request.fileId}";
        url = $"{url}?ids={string.Join(",", request.ids)}";

        if (!string.IsNullOrEmpty(request.version))
            url = $"{url}&version={request.version}";

        if (!string.IsNullOrEmpty(request.format))
            url = $"{url}&format={request.format}";

        if (request.scale.HasValue)
            url = $"{url}&scale={request.scale.Value}";

        url = $"{url}&svg_include_id={request.svgIncludeId.ToString().ToLower()}";
        url = $"{url}&svg_simplify_stroke={request.svgSimplifyStroke.ToString().ToLower()}";
        url = $"{url}&use_absolute_bounds={request.useAbsoluteBounds.ToString().ToLower()}";
        return url;
    }

    private async Task<byte[]> GetBytesAsync(
        string url,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Note: HttpClient is a shared static singleton. Dispose is intentionally a no-op
    /// to prevent closing the shared socket. The OS reclaims resources on process exit.
    /// </summary>
    public void Dispose() { }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// Handles image loading operations — download from URL, load from base64, caching.
/// Sprite generation (pixel rendering) has been removed.
/// </summary>
public static class ImageRenderer
{
    private const int MAX_IMAGE_RESOLUTION = 4096;

    // Cache for downloaded images
    private static Dictionary<string, Texture2D> _downloadedImageCache =
        new Dictionary<string, Texture2D>();

    public static Texture2D DownloadImageFromUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return null;
        }

        // Check cache first
        if (_downloadedImageCache.TryGetValue(imageUrl, out Texture2D cachedTexture))
        {
            return cachedTexture;
        }

        try
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "Unity-FigmaConverter");
                byte[] imageData = client.DownloadData(imageUrl);

                if (imageData == null || imageData.Length == 0)
                {
                    return null;
                }

                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    // Cache the texture
                    _downloadedImageCache[imageUrl] = texture;

                    return texture;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error downloading image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads an image from a URL asynchronously
    /// </summary>
    public static IEnumerator DownloadImageFromUrlAsync(
        string imageUrl,
        System.Action<Texture2D> onComplete,
        int maxTextureSize = 2048,
        string mainNodeId = null,
        string imageName = null
    )
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("ImageRenderer: Image URL is null or empty");
            onComplete?.Invoke(null);
            yield break;
        }

        // Check cache first
        if (_downloadedImageCache.TryGetValue(imageUrl, out Texture2D cachedTexture))
        {
            onComplete?.Invoke(cachedTexture);
            yield break;
        }

        string highResUrl = GetHighResolutionImageUrl(imageUrl, maxTextureSize);

        using (
            UnityEngine.Networking.UnityWebRequest request =
                UnityEngine.Networking.UnityWebRequestTexture.GetTexture(highResUrl)
        )
        {
            request.SetRequestHeader("User-Agent", "Unity-FigmaConverter");

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(
                    request
                );
                if (texture != null)
                {
                    texture.Apply();

                    if (!texture.isReadable)
                    {
                        Debug.LogError(
                            $"ImageRenderer: Async downloaded texture is not readable from: {imageUrl}"
                        );
                        UnityEngine.Object.DestroyImmediate(texture);
                        onComplete?.Invoke(null);
                        yield break;
                    }

                    // Save image to Resources if mainNodeId and imageName are provided
                    if (!string.IsNullOrEmpty(mainNodeId) && !string.IsNullOrEmpty(imageName))
                    {
                        SpriteSaver.SaveImageToResources(texture, imageName, mainNodeId);
                    }

                    // Cache the downloaded image
                    _downloadedImageCache[imageUrl] = texture;

                    Debug.Log(
                        $"ImageRenderer: Successfully downloaded image from: {imageUrl} ({texture.width}x{texture.height})"
                    );
                    onComplete?.Invoke(texture);
                }
                else
                {
                    Debug.LogError(
                        $"ImageRenderer: Downloaded texture is null for URL: {imageUrl}"
                    );
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError(
                    $"ImageRenderer: Failed to download image from {imageUrl}: {request.error}"
                );
                onComplete?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Loads an image from base64 data
    /// </summary>
    public static Texture2D LoadImageFromBase64(string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            Debug.LogError("ImageRenderer: Base64 data is null or empty");
            return null;
        }

        try
        {
            // Remove data URL prefix if present
            if (base64Data.StartsWith("data:"))
            {
                int commaIndex = base64Data.IndexOf(',');
                if (commaIndex >= 0)
                {
                    base64Data = base64Data.Substring(commaIndex + 1);
                }
            }

            byte[] imageData = Convert.FromBase64String(base64Data);

            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogError("ImageRenderer: Decoded image data is null or empty");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageData))
            {
                return texture;
            }
            else
            {
                Debug.LogError("ImageRenderer: Failed to load image data into texture");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error loading image from base64 data: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts image data bytes to base64 string
    /// </summary>
    public static string ConvertImageDataToBase64(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return string.Empty;

        try
        {
            return Convert.ToBase64String(imageData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error converting image data to base64: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Clears the image cache
    /// </summary>
    public static void ClearImageCache()
    {
        foreach (var texture in _downloadedImageCache.Values)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        _downloadedImageCache.Clear();
        Debug.Log("ImageRenderer: Image cache cleared");
    }

    /// <summary>
    /// Gets the high resolution image URL from a Figma image URL
    /// </summary>
    public static string GetHighResolutionImageUrl(
        string originalUrl,
        int maxSize = MAX_IMAGE_RESOLUTION
    )
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        if (originalUrl.Contains("&scale="))
        {
            return originalUrl.Replace("&scale=1", $"&scale={maxSize / 1000f}");
        }
        else if (originalUrl.Contains("?"))
        {
            return $"{originalUrl}&scale={maxSize / 1000f}";
        }
        else
        {
            return $"{originalUrl}?scale={maxSize / 1000f}";
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Handles rendering of image fills and image-related operations
/// Manages image downloads, caching, and rendering to pixel arrays
/// </summary>
public static class ImageRenderer
{
    // Cache for downloaded images
    private static Dictionary<string, Texture2D> _downloadedImageCache =
        new Dictionary<string, Texture2D>();

    /// <summary>
    /// Renders an image fill for a shape
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="imageData">Image data dictionary</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    public static void RenderImageFill(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData,
        string mainNodeId
    )
    {
        if (nodeData == null || pixels == null || imageData == null)
            return;

        // Get image fill data
        JObject imageFill = GetImageFill(nodeData);
        if (imageFill == null)
            return;

        // Get image reference
        string imageRef = imageFill["imageRef"]?.ToString();
        if (string.IsNullOrEmpty(imageRef))
            return;

        // Get image data
        if (
            !imageData.TryGetValue(imageRef, out string base64Data)
            || string.IsNullOrEmpty(base64Data)
        )
        {
            Debug.LogWarning($"ImageRenderer: No image data found for reference {imageRef}");
            return;
        }

        // Load image texture
        Texture2D imageTexture = LoadImageFromBase64(base64Data);
        if (imageTexture == null)
        {
            Debug.LogError(
                $"ImageRenderer: Failed to load image from base64 data for reference {imageRef}"
            );
            return;
        }

        // Get image transform properties
        string scaleMode = imageFill["scaleMode"]?.ToString() ?? "FILL";
        float scaleX = imageFill["scaleX"]?.Value<float>() ?? 1f;
        float scaleY = imageFill["scaleY"]?.Value<float>() ?? 1f;
        float rotation = imageFill["rotation"]?.Value<float>() ?? 0f;
        float translationX = imageFill["translationX"]?.Value<float>() ?? 0f;
        float translationY = imageFill["translationY"]?.Value<float>() ?? 0f;
        float opacity = imageFill["opacity"]?.Value<float>() ?? 1f;

        // Create shape mask
        bool[] shapeMask = ShapeMaskGenerator.CreateMask(nodeData, (int)width, (int)height);

        // Render image to pixels
        RenderImageToPixels(
            imageTexture,
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            scaleMode,
            scaleX,
            scaleY,
            rotation,
            translationX,
            translationY,
            opacity,
            shapeMask
        );
    }

    /// <summary>
    /// Renders an image to a pixel array with transformations
    /// </summary>
    /// <param name="imageTexture">Source image texture</param>
    /// <param name="pixels">Target pixel array</param>
    /// <param name="textureWidth">Width of the target texture</param>
    /// <param name="textureHeight">Height of the target texture</param>
    /// <param name="nodeWidth">Width of the node</param>
    /// <param name="nodeHeight">Height of the node</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="scaleMode">Image scale mode</param>
    /// <param name="scaleX">X scale factor</param>
    /// <param name="scaleY">Y scale factor</param>
    /// <param name="rotation">Rotation in degrees</param>
    /// <param name="translationX">X translation</param>
    /// <param name="translationY">Y translation</param>
    /// <param name="opacity">Opacity of the image</param>
    /// <param name="shapeMask">Shape mask for clipping</param>
    public static void RenderImageToPixels(
        Texture2D imageTexture,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY,
        string scaleMode,
        float scaleX,
        float scaleY,
        float rotation,
        float translationX,
        float translationY,
        float opacity,
        bool[] shapeMask
    )
    {
        if (imageTexture == null)
        {
            Debug.LogError("ImageRenderer: imageTexture is null");
            return;
        }

        if (!imageTexture.isReadable)
        {
            Debug.LogError($"ImageRenderer: imageTexture '{imageTexture.name}' is not readable");
            return;
        }

        Color[] imagePixels;
        int imageWidth;
        int imageHeight;

        try
        {
            imagePixels = imageTexture.GetPixels();
            imageWidth = imageTexture.width;
            imageHeight = imageTexture.height;

            Debug.Log(
                $"ImageRenderer: Rendering {imageWidth}x{imageHeight} image to {textureWidth}x{textureHeight} texture"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Failed to get pixels from texture: {ex.Message}");
            return;
        }

        // Calculate scaling based on scale mode
        float finalScaleX = scaleX;
        float finalScaleY = scaleY;

        switch (scaleMode)
        {
            case "FILL":
                // Scale to fill the entire node
                finalScaleX = nodeWidth / imageWidth;
                finalScaleY = nodeHeight / imageHeight;
                break;

            case "FIT":
                // Scale to fit within the node
                float fitScale = Mathf.Min(nodeWidth / imageWidth, nodeHeight / imageHeight);
                finalScaleX = finalScaleY = fitScale;
                break;

            case "CROP":
                // Scale to fill, cropping if necessary
                float cropScale = Mathf.Max(nodeWidth / imageWidth, nodeHeight / imageHeight);
                finalScaleX = finalScaleY = cropScale;
                break;

            case "TILE":
                // Use original scale for tiling
                finalScaleX = finalScaleY = 1f;
                break;
        }

        // Render image pixels
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                // Check if pixel is within node bounds
                if (
                    x < offsetX
                    || x >= offsetX + nodeWidth
                    || y < offsetY
                    || y >= offsetY + nodeHeight
                )
                    continue;

                // Check shape mask if provided
                int shapeIndex = (y - offsetY) * (int)nodeWidth + (x - offsetX);
                if (shapeMask != null && shapeIndex < shapeMask.Length && !shapeMask[shapeIndex])
                    continue;

                // Calculate image coordinates
                float localX = (x - offsetX) - nodeWidth * 0.5f;
                float localY = (y - offsetY) - nodeHeight * 0.5f;

                // Apply rotation
                if (rotation != 0)
                {
                    float cos = Mathf.Cos(rotation * Mathf.Deg2Rad);
                    float sin = Mathf.Sin(rotation * Mathf.Deg2Rad);
                    float rotatedX = localX * cos - localY * sin;
                    float rotatedY = localX * sin + localY * cos;
                    localX = rotatedX;
                    localY = rotatedY;
                }

                // Apply translation
                localX += translationX;
                localY += translationY;

                // Apply scaling
                float imageX = (localX / finalScaleX) + imageWidth * 0.5f;
                float imageY = (localY / finalScaleY) + imageHeight * 0.5f;

                // Handle tiling
                if (scaleMode == "TILE")
                {
                    imageX = ((x - offsetX) % imageWidth + imageWidth) % imageWidth;
                    imageY = ((y - offsetY) % imageHeight + imageHeight) % imageHeight;
                }

                // Sample image pixel
                if (imageX >= 0 && imageX < imageWidth && imageY >= 0 && imageY < imageHeight)
                {
                    int imagePixelX = Mathf.FloorToInt(imageX);
                    int imagePixelY = Mathf.FloorToInt(imageY);

                    // Clamp to image bounds
                    imagePixelX = Mathf.Clamp(imagePixelX, 0, imageWidth - 1);
                    imagePixelY = Mathf.Clamp(imagePixelY, 0, imageHeight - 1);

                    int imagePixelIndex = imagePixelY * imageWidth + imagePixelX;
                    if (imagePixelIndex < imagePixels.Length)
                    {
                        Color imageColor = imagePixels[imagePixelIndex];
                        imageColor.a *= opacity; // Apply opacity

                        int pixelIndex = y * textureWidth + x;
                        if (pixelIndex < pixels.Length)
                        {
                            pixels[pixelIndex] = imageColor;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Downloads an image from a URL
    /// </summary>
    /// <param name="imageUrl">URL of the image to download</param>
    /// <returns>Downloaded texture or null if failed</returns>
    public static Texture2D DownloadImageFromUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("ImageRenderer: Image URL is null or empty");
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
                    Debug.LogError(
                        $"ImageRenderer: Downloaded image data is null or empty for URL: {imageUrl}"
                    );
                    return null;
                }

                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    // Cache the texture
                    _downloadedImageCache[imageUrl] = texture;
                    Debug.Log(
                        $"ImageRenderer: Successfully downloaded and cached image from {imageUrl}"
                    );
                    return texture;
                }
                else
                {
                    Debug.LogError(
                        $"ImageRenderer: Failed to load image data into texture for URL: {imageUrl}"
                    );
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error downloading image from {imageUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads an image from a URL asynchronously
    /// </summary>
    /// <param name="imageUrl">URL of the image to download</param>
    /// <param name="onComplete">Callback when download is complete</param>
    /// <returns>Coroutine for the download</returns>
    public static IEnumerator DownloadImageFromUrlAsync(
        string imageUrl,
        System.Action<Texture2D> onComplete
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

        using (
            UnityEngine.Networking.UnityWebRequest request =
                UnityEngine.Networking.UnityWebRequestTexture.GetTexture(imageUrl)
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
                    // Cache the texture
                    _downloadedImageCache[imageUrl] = texture;
                    Debug.Log(
                        $"ImageRenderer: Successfully downloaded and cached image from {imageUrl}"
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
    /// <param name="base64Data">Base64 encoded image data</param>
    /// <returns>Loaded texture or null if failed</returns>
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
                Debug.Log(
                    $"ImageRenderer: Successfully loaded image from base64 data ({imageData.Length} bytes)"
                );
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
    /// Gets image fill data from node data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <returns>Image fill data or null if not found</returns>
    private static JObject GetImageFill(JObject nodeData)
    {
        if (nodeData == null)
            return null;

        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return null;

        foreach (JObject fill in fills)
        {
            string fillType = fill["type"]?.ToString();
            if (fillType == "IMAGE")
            {
                return fill;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts image data to base64 string
    /// </summary>
    /// <param name="imageData">Image data bytes</param>
    /// <returns>Base64 encoded string</returns>
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
    /// Cleans up Figma image data dictionary by removing data URL prefixes
    /// </summary>
    /// <param name="figmaImageData">Figma image data dictionary with data URLs</param>
    /// <returns>Cleaned image data dictionary with pure base64 strings</returns>
    public static Dictionary<string, string> CleanFigmaImageData(
        Dictionary<string, string> figmaImageData
    )
    {
        if (figmaImageData == null)
            return new Dictionary<string, string>();

        Dictionary<string, string> convertedData = new Dictionary<string, string>();

        foreach (var kvp in figmaImageData)
        {
            string key = kvp.Key;
            string value = kvp.Value;

            // Remove data URL prefix if present
            if (value.StartsWith("data:"))
            {
                int commaIndex = value.IndexOf(',');
                if (commaIndex >= 0)
                {
                    value = value.Substring(commaIndex + 1);
                }
            }

            convertedData[key] = value;
        }

        return convertedData;
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
    /// <param name="originalUrl">Original Figma image URL</param>
    /// <param name="maxSize">Maximum size for the image</param>
    /// <returns>High resolution image URL</returns>
    public static string GetHighResolutionImageUrl(
        string originalUrl,
        int maxSize = SpriteGenerationConstants.MAX_IMAGE_RESOLUTION
    )
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        // Figma image URLs can be modified to get different sizes
        // Replace the size parameter in the URL
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

    /// <summary>
    /// Upscales a texture to a maximum size while maintaining aspect ratio
    /// </summary>
    /// <param name="originalTexture">Original texture</param>
    /// <param name="maxSize">Maximum size for the upscaled texture</param>
    /// <returns>Upscaled texture</returns>
    public static Texture2D UpscaleTexture(
        Texture2D originalTexture,
        int maxSize = SpriteGenerationConstants.MAX_IMAGE_RESOLUTION
    )
    {
        if (originalTexture == null)
            return null;

        int originalWidth = originalTexture.width;
        int originalHeight = originalTexture.height;

        // Calculate new size maintaining aspect ratio
        float aspectRatio = (float)originalWidth / originalHeight;
        int newWidth,
            newHeight;

        if (originalWidth > originalHeight)
        {
            newWidth = Mathf.Min(maxSize, originalWidth);
            newHeight = Mathf.RoundToInt(newWidth / aspectRatio);
        }
        else
        {
            newHeight = Mathf.Min(maxSize, originalHeight);
            newWidth = Mathf.RoundToInt(newHeight * aspectRatio);
        }

        // If no upscaling needed, return original
        if (newWidth == originalWidth && newHeight == originalHeight)
            return originalTexture;

        // Create new texture and scale
        Texture2D upscaledTexture = new Texture2D(
            newWidth,
            newHeight,
            originalTexture.format,
            false
        );

        // Get original pixels
        Color[] originalPixels = originalTexture.GetPixels();
        Color[] newPixels = new Color[newWidth * newHeight];

        // Simple nearest neighbor scaling
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int sourceX = Mathf.RoundToInt((float)x / newWidth * originalWidth);
                int sourceY = Mathf.RoundToInt((float)y / newHeight * originalHeight);

                sourceX = Mathf.Clamp(sourceX, 0, originalWidth - 1);
                sourceY = Mathf.Clamp(sourceY, 0, originalHeight - 1);

                int sourceIndex = sourceY * originalWidth + sourceX;
                int newIndex = y * newWidth + x;

                newPixels[newIndex] = originalPixels[sourceIndex];
            }
        }

        upscaledTexture.SetPixels(newPixels);
        upscaledTexture.Apply();

        Debug.Log(
            $"ImageRenderer: Upscaled texture from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}"
        );

        return upscaledTexture;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Refactored DirectSpriteGenerator using service-based architecture
/// Orchestrates sprite generation using specialized services
/// </summary>
public static class SpriteGenerator
{
    /// <summary>
    /// Generates sprite directly from Figma node without SVG intermediate
    /// Uses the new service-based architecture for better maintainability
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the sprite</param>
    /// <param name="height">Height of the sprite</param>
    /// <param name="imageData">Image data dictionary</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    /// <returns>Generated sprite or null if failed</returns>
    public static Sprite GenerateSpriteFromNodeDirect(
        JObject nodeData,
        float width,
        float height,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        try
        {
            string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
            Debug.Log(
                $"DirectSpriteGeneratorRefactored: Generating sprite for {nodeName} ({width}x{height})"
            );

            // Log image data availability
            LogImageDataInfo(imageData);

            // Step 1: Calculate effect padding using EffectRenderer
            Vector4 effectPadding = EffectRenderer.CalculateEffectPaddingDirectional(nodeData);

            // Step 2: Create optimal texture using SpriteTextureManager
            Texture2D texture = SpriteTextureManager.CreateOptimalTexture(
                width,
                height,
                effectPadding
            );
            if (texture == null)
            {
                Debug.LogError("DirectSpriteGeneratorRefactored: Failed to create texture");
                return null;
            }

            // Step 3: Initialize pixels using SpriteTextureManager
            Color[] pixels = SpriteTextureManager.InitializePixels(
                texture.width * texture.height,
                SpriteGenerationConstants.TRANSPARENT_COLOR
            );

            // Step 4: Calculate offsets for positioning
            int offsetX = (int)effectPadding.x; // Left padding
            int offsetY = (int)effectPadding.z; // Top padding

            // Step 5: Render effects first (shadows, etc.)
            EffectRenderer.RenderDropShadows(
                nodeData,
                pixels,
                texture.width,
                texture.height,
                width,
                height,
                offsetX,
                offsetY
            );

            // Step 6: Render the main shape using ShapeRenderer
            RenderMainShape(
                nodeData,
                pixels,
                texture.width,
                texture.height,
                width,
                height,
                offsetX,
                offsetY,
                imageData,
                mainNodeId
            );

            // Step 7: Apply pixels to texture using SpriteTextureManager
            SpriteTextureManager.ApplyPixelsToTexture(texture, pixels);

            // Step 8: Calculate content bounds using SpriteTextureManager
            Rect contentBounds = SpriteTextureManager.CalculateContentBounds(
                pixels,
                texture.width,
                texture.height,
                offsetX,
                offsetY,
                width,
                height,
                effectPadding
            );

            // Step 9: Create sprite using SpriteTextureManager
            Sprite sprite = SpriteTextureManager.CreateSpriteFromTexture(texture, contentBounds);
            if (sprite == null)
            {
                Debug.LogError("DirectSpriteGeneratorRefactored: Failed to create sprite");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            // Step 10: Save sprite to Resources using SpriteSaver
            if (!string.IsNullOrEmpty(mainNodeId))
            {
                SpriteSaver.SaveSpriteToResources(sprite, nodeName, mainNodeId);
            }

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"DirectSpriteGeneratorRefactored: Error generating sprite: {ex.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Generates sprite directly from Figma node with async image download support
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the sprite</param>
    /// <param name="height">Height of the sprite</param>
    /// <param name="imageData">Image data dictionary</param>
    /// <param name="onComplete">Callback when generation is complete</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    /// <returns>Coroutine for async generation</returns>
    public static IEnumerator GenerateSpriteFromNodeDirectAsync(
        JObject nodeData,
        float width,
        float height,
        Dictionary<string, string> imageData,
        System.Action<Sprite> onComplete,
        string mainNodeId = null
    )
    {
        // Check if node has image fills that need async download
        if (HasImageFill(nodeData))
        {
            JObject imageFill = GetImageFill(nodeData);
            if (imageFill != null)
            {
                string imageUrl = imageFill["imageUrl"]?.ToString();
                string imageRef = imageFill["imageRef"]?.ToString();
                string nodeId = nodeData["id"]?.ToString();
                string nodeName = nodeData["name"]?.ToString() ?? "Unknown";

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    // Download image asynchronously from URL with higher resolution and save to Resources
                    yield return DownloadImageFromUrlAsync(
                        imageUrl,
                        (downloadedTexture) =>
                        {
                            if (downloadedTexture != null)
                            {
                                // Try to load sprite from Resources first
                                Sprite savedSprite = LoadSpriteFromResources(nodeName, mainNodeId);
                                if (savedSprite != null)
                                {
                                    onComplete?.Invoke(savedSprite);
                                }
                                else
                                {
                                    // Generate sprite with downloaded image
                                    Sprite sprite = GenerateSpriteFromNodeDirect(
                                        nodeData,
                                        width,
                                        height,
                                        imageData,
                                        mainNodeId
                                    );
                                    onComplete?.Invoke(sprite);
                                }
                            }
                            else
                            {
                                Debug.LogError(
                                    "DirectSpriteGeneratorRefactored: Failed to download image from URL, generating sprite without image fill"
                                );
                                // Generate sprite without image fill
                                Sprite sprite = GenerateSpriteFromNodeDirect(
                                    nodeData,
                                    width,
                                    height,
                                    imageData,
                                    mainNodeId
                                );
                                onComplete?.Invoke(sprite);
                            }
                        },
                        2048, // Max texture size for high resolution
                        nodeId,
                        nodeName
                    );
                    yield break;
                }
                else if (!string.IsNullOrEmpty(imageRef))
                {
                    // Check if we have image data for this imageRef
                    if (imageData != null && imageData.ContainsKey(imageRef))
                    {
                        // We have the image data, generate sprite normally
                        Sprite sprite = GenerateSpriteFromNodeDirect(
                            nodeData,
                            width,
                            height,
                            imageData,
                            mainNodeId
                        );
                        onComplete?.Invoke(sprite);
                        yield break;
                    }
                    else
                    {
                        Debug.LogError(
                            $"DirectSpriteGeneratorRefactored: Image data not found for imageRef: {imageRef}"
                        );
                        // Generate sprite without image fill
                        Sprite sprite = GenerateSpriteFromNodeDirect(
                            nodeData,
                            width,
                            height,
                            imageData,
                            mainNodeId
                        );
                        onComplete?.Invoke(sprite);
                        yield break;
                    }
                }
            }
        }

        // No image fills or no async download needed, generate sprite normally
        Sprite normalSprite = GenerateSpriteFromNodeDirect(
            nodeData,
            width,
            height,
            imageData,
            mainNodeId
        );
        onComplete?.Invoke(normalSprite);
    }

    /// <summary>
    /// Generates sprite from Resources or creates new one if not found
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the sprite</param>
    /// <param name="height">Height of the sprite</param>
    /// <param name="imageData">Image data dictionary</param>
    /// <param name="onComplete">Callback when generation is complete</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    /// <returns>Coroutine for async generation</returns>
    public static IEnumerator GenerateSpriteFromResourcesOrCreateAsync(
        JObject nodeData,
        float width,
        float height,
        Dictionary<string, string> imageData,
        System.Action<Sprite> onComplete,
        string mainNodeId = null
    )
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "Unknown";

        // First try to load from Resources using SpriteSaver
        Sprite savedSprite = SpriteSaver.LoadSpriteFromResourcesWithMainNodeId(
            nodeName,
            nodeId,
            mainNodeId
        );
        if (savedSprite != null)
        {
            Debug.Log(
                $"DirectSpriteGeneratorRefactored: Using saved sprite from Resources for {nodeName}"
            );
            onComplete?.Invoke(savedSprite);
            yield break;
        }

        // If not found in Resources, generate new sprite
        Debug.Log(
            $"DirectSpriteGeneratorRefactored: Sprite not found in Resources, generating new sprite for {nodeName}"
        );
        yield return GenerateSpriteFromNodeDirectAsync(
            nodeData,
            width,
            height,
            imageData,
            (generatedSprite) =>
            {
                // The sprite is already saved in GenerateSpriteFromNodeDirect
                onComplete?.Invoke(generatedSprite);
            },
            mainNodeId
        );
    }

    /// <summary>
    /// Downloads an image from URL using ImageRenderer
    /// </summary>
    /// <param name="imageUrl">URL of the image to download</param>
    /// <returns>Downloaded texture or null if failed</returns>
    public static Texture2D DownloadImageFromUrl(string imageUrl)
    {
        return ImageRenderer.DownloadImageFromUrl(imageUrl);
    }

    /// <summary>
    /// Downloads an image from URL asynchronously using ImageRenderer
    /// </summary>
    /// <param name="imageUrl">URL of the image to download</param>
    /// <param name="onComplete">Callback when download is complete</param>
    /// <param name="maxTextureSize">Maximum texture size for high resolution</param>
    /// <param name="mainNodeId">Main node ID from config for saving to Resources</param>
    /// <param name="imageName">Image name for saving to Resources</param>
    /// <returns>Coroutine for the download</returns>
    public static IEnumerator DownloadImageFromUrlAsync(
        string imageUrl,
        System.Action<Texture2D> onComplete,
        int maxTextureSize = 2048,
        string mainNodeId = null,
        string imageName = null
    )
    {
        yield return ImageRenderer.DownloadImageFromUrlAsync(
            imageUrl,
            onComplete,
            maxTextureSize,
            mainNodeId,
            imageName
        );
    }

    /// <summary>
    /// Loads a sprite from Resources using SpriteSaver
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="mainNodeId">Main node ID from config for file organization</param>
    /// <returns>Loaded sprite or null if not found</returns>
    public static Sprite LoadSpriteFromResources(string imageName, string mainNodeId)
    {
        return SpriteSaver.LoadSpriteFromResources(imageName, mainNodeId);
    }

    /// <summary>
    /// Loads a sprite from Resources with main node ID fallback using SpriteSaver
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="instanceNodeId">Instance node ID</param>
    /// <param name="mainNodeId">Main node ID for fallback</param>
    /// <returns>Loaded sprite or null if not found</returns>
    public static Sprite LoadSpriteFromResourcesWithMainNodeId(
        string imageName,
        string instanceNodeId,
        string mainNodeId
    )
    {
        return SpriteSaver.LoadSpriteFromResourcesWithMainNodeId(
            imageName,
            instanceNodeId,
            mainNodeId
        );
    }

    /// <summary>
    /// Clears the image cache using ImageRenderer
    /// </summary>
    public static void ClearImageCache()
    {
        ImageRenderer.ClearImageCache();
    }

    /// <summary>
    /// Converts image data to base64 using ImageRenderer
    /// </summary>
    /// <param name="imageData">Image data bytes</param>
    /// <returns>Base64 encoded string</returns>
    public static string ConvertImageDataToBase64(byte[] imageData)
    {
        return ImageRenderer.ConvertImageDataToBase64(imageData);
    }

    /// <summary>
    /// Converts FigmaApi image data dictionary to DirectSpriteGenerator format
    /// </summary>
    /// <param name="figmaImageData">Figma image data dictionary with byte[] values</param>
    /// <returns>Converted image data dictionary with base64 string values</returns>
    public static Dictionary<string, string> ConvertFigmaImageData(
        Dictionary<string, byte[]> figmaImageData
    )
    {
        if (figmaImageData == null)
            return null;

        var convertedData = new Dictionary<string, string>();
        foreach (var kvp in figmaImageData)
        {
            if (kvp.Value != null)
            {
                convertedData[kvp.Key] = ConvertImageDataToBase64(kvp.Value);
            }
        }
        return convertedData;
    }

    #region Private Helper Methods

    /// <summary>
    /// Renders the main shape based on node type
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
    private static void RenderMainShape(
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
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        switch (nodeType)
        {
            case "RECTANGLE":
            case "ROUNDED_RECTANGLE":
            case "FRAME":
                ShapeRenderer.RenderRectangle(
                    nodeData,
                    pixels,
                    textureWidth,
                    textureHeight,
                    width,
                    height,
                    offsetX,
                    offsetY,
                    imageData,
                    mainNodeId
                );
                break;

            case "ELLIPSE":
                ShapeRenderer.RenderEllipse(
                    nodeData,
                    pixels,
                    textureWidth,
                    textureHeight,
                    width,
                    height,
                    offsetX,
                    offsetY,
                    imageData,
                    mainNodeId
                );
                break;

            default:
                // Default to rectangle for unknown types
                ShapeRenderer.RenderRectangle(
                    nodeData,
                    pixels,
                    textureWidth,
                    textureHeight,
                    width,
                    height,
                    offsetX,
                    offsetY,
                    imageData,
                    mainNodeId
                );
                break;
        }
    }

    /// <summary>
    /// Checks if a node has image fills
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <returns>True if node has image fills</returns>
    private static bool HasImageFill(JObject nodeData)
    {
        if (nodeData == null)
            return false;

        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return false;

        foreach (JObject fill in fills)
        {
            bool visible = fill["visible"]?.ToObject<bool>() ?? true;
            if (visible)
            {
                string fillType = fill["type"]?.ToString();
                if (fillType == "IMAGE")
                {
                    Debug.Log($"Node has image fill: {fill["imageRef"]?.ToString()}");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the first image fill from Figma node data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <returns>First image fill or null if not found</returns>
    private static JObject GetImageFill(JObject nodeData)
    {
        if (nodeData == null)
            return null;

        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return null;

        foreach (JObject fill in fills)
        {
            bool visible = fill["visible"]?.ToObject<bool>() ?? true;
            if (visible)
            {
                string fillType = fill["type"]?.ToString();
                if (fillType == "IMAGE")
                {
                    return fill;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Downloads images asynchronously for a node
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="imageData">Image data dictionary</param>
    /// <param name="onComplete">Callback when download is complete</param>
    /// <returns>Coroutine for async download</returns>
    private static IEnumerator DownloadImagesAsync(
        JObject nodeData,
        Dictionary<string, string> imageData,
        System.Action<bool> onComplete
    )
    {
        bool allDownloadsSuccessful = true;

        // Get all image fills from the node
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null)
        {
            foreach (JObject fill in fills)
            {
                string fillType = fill["type"]?.ToString();
                if (fillType == "IMAGE")
                {
                    string imageRef = fill["imageRef"]?.ToString();
                    if (!string.IsNullOrEmpty(imageRef) && imageData.ContainsKey(imageRef))
                    {
                        // Image data is already available in base64 format
                        // No need to download
                        continue;
                    }
                }
            }
        }

        // For now, assume all images are available in imageData
        // In the future, this could be enhanced to download missing images
        onComplete?.Invoke(allDownloadsSuccessful);
        yield break;
    }

    /// <summary>
    /// Logs information about available image data
    /// </summary>
    /// <param name="imageData">Image data dictionary</param>
    private static void LogImageDataInfo(Dictionary<string, string> imageData)
    {
        if (imageData != null)
        {
            Debug.Log(
                $"DirectSpriteGeneratorRefactored: Image data available: {imageData.Count} entries"
            );
            foreach (var kvp in imageData)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value?.Length ?? 0} characters");
            }
        }
        else
        {
            Debug.Log("DirectSpriteGeneratorRefactored: No image data provided");
        }
    }

    #endregion
}

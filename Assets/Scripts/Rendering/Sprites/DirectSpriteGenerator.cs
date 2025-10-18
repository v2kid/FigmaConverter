using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class DirectSpriteGenerator
{
    private const int DEFAULT_TEXTURE_SIZE = 512;
    private const float PIXELS_PER_UNIT = 100f;

    // Cache for downloaded images
    private static Dictionary<string, Texture2D> _downloadedImageCache =
        new Dictionary<string, Texture2D>();

    /// <summary>
    /// Generates sprite directly from Figma node without SVG intermediate
    /// </summary>
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
                $"DirectSpriteGenerator: Generating sprite for {nodeName} ({width}x{height})"
            );

            if (imageData != null)
            {
                Debug.Log(
                    $"DirectSpriteGenerator: Image data available: {imageData.Count} entries"
                );
                foreach (var kvp in imageData)
                {
                    Debug.Log($"  - {kvp.Key}: {kvp.Value?.Length ?? 0} characters");
                }
            }
            else
            {
                Debug.Log("DirectSpriteGenerator: No image data provided");
            }
            // Calculate directional effect padding for drop shadows
            Vector4 effectPadding = CalculateEffectPaddingDirectional(nodeData);

            // Calculate texture size based on dimensions + directional padding
            int textureWidth = Mathf.NextPowerOfTwo(
                (int)(width + effectPadding.x + effectPadding.y)
            );
            int textureHeight = Mathf.NextPowerOfTwo(
                (int)(height + effectPadding.z + effectPadding.w)
            );
            textureWidth = Mathf.Max(textureWidth, 64);
            textureHeight = Mathf.Max(textureHeight, 64);
            textureWidth = Mathf.Min(textureWidth, 2048);
            textureHeight = Mathf.Min(textureHeight, 2048);

            // Create texture
            Texture2D texture = new Texture2D(
                textureWidth,
                textureHeight,
                TextureFormat.RGBA32,
                false
            );
            Color[] pixels = new Color[textureWidth * textureHeight];

            // Initialize to transparent
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            // Calculate offset for positioning the shape with directional padding
            int offsetX = (int)effectPadding.x; // Left padding
            int offsetY = (int)effectPadding.z; // Top padding

            // Get node type and render accordingly
            string nodeType = nodeData["type"]?.ToString()?.ToUpper();

            // First render drop shadows if any
            RenderDropShadows(
                nodeData,
                pixels,
                textureWidth,
                textureHeight,
                width,
                height,
                offsetX,
                offsetY
            );

            // Then render the main shape
            switch (nodeType)
            {
                case "RECTANGLE":
                case "ROUNDED_RECTANGLE":
                case "FRAME":
                    RenderRectangle(
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
                    RenderEllipse(
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
                    RenderRectangle(
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

            // Apply pixels to texture
            texture.SetPixels(pixels);
            texture.Apply();

            // Calculate actual content bounds (excluding padding)
            Rect contentBounds = CalculateContentBounds(
                pixels,
                textureWidth,
                textureHeight,
                offsetX,
                offsetY,
                width,
                height,
                effectPadding
            );

            // Create sprite with actual content bounds
            Sprite sprite = Sprite.Create(
                texture,
                contentBounds,
                new Vector2(0.5f, 0.5f),
                PIXELS_PER_UNIT
            );

            // Save sprite to Resources if nodeId is available
            string nodeId = nodeData["id"]?.ToString();
            if (!string.IsNullOrEmpty(nodeId))
            {
                SaveSpriteToResources(sprite, nodeName, nodeId);
            }

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating sprite directly: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates sprite directly from Figma node with async image download support
    /// </summary>
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
                                Sprite savedSprite = LoadSpriteFromResources(nodeName, nodeId);
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
                                    "Failed to download image from URL, generating sprite without image fill"
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
                        Debug.LogError($"Image data not found for imageRef: {imageRef}");
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
        Sprite normalSprite = GenerateSpriteFromNodeDirect(nodeData, width, height, imageData, mainNodeId);
        onComplete?.Invoke(normalSprite);
    }

    /// <summary>
    /// Generates sprite from saved Resources or creates new one if not found
    /// </summary>
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

        // First try to load from Resources using mainNodeId if available
        Sprite savedSprite = LoadSpriteFromResourcesWithMainNodeId(nodeName, nodeId, mainNodeId);
        if (savedSprite != null)
        {
            Debug.Log($"Using saved sprite from Resources for {nodeName}");
            onComplete?.Invoke(savedSprite);
            yield break;
        }

        // If not found in Resources, generate new sprite
        Debug.Log($"Sprite not found in Resources, generating new sprite for {nodeName}");
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
    /// Calculates directional padding needed for effects like drop shadows
    /// Returns Vector4(left, right, top, bottom)
    /// </summary>
    private static Vector4 CalculateEffectPaddingDirectional(JObject nodeData)
    {
        JArray effects = nodeData["effects"] as JArray;
        if (effects == null || effects.Count == 0)
            return Vector4.zero;

        float left = 0f,
            right = 0f,
            top = 0f,
            bottom = 0f;

        foreach (JObject effect in effects)
        {
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
                continue;

            string effectType = effect["type"]?.ToString();
            if (
                effectType != "DROP_SHADOW"
                && effectType != "LAYER_BLUR"
                && effectType != "BACKGROUND_BLUR"
            )
                continue;

            float radius = effect["radius"]?.ToObject<float>() ?? 0f;
            JObject offset = effect["offset"] as JObject;
            float offsetX = offset?["x"]?.ToObject<float>() ?? 0f;
            float offsetY = offset?["y"]?.ToObject<float>() ?? 0f;

            switch (effectType)
            {
                case "DROP_SHADOW":
                    left = Mathf.Max(left, Mathf.Max(0, -offsetX) + radius);
                    right = Mathf.Max(right, Mathf.Max(0, offsetX) + radius);
                    top = Mathf.Max(top, Mathf.Max(0, -offsetY) + radius);
                    bottom = Mathf.Max(bottom, Mathf.Max(0, offsetY) + radius);
                    break;

                case "LAYER_BLUR":
                case "BACKGROUND_BLUR":
                    // Blur lan đều ra mọi hướng
                    left = right = top = bottom = Mathf.Max(left, radius * 2f);
                    break;
            }
        }

        return new Vector4(left, right, bottom, top);
    }

    /// <summary>
    /// Calculates the actual content bounds of the sprite (excluding padding)
    /// </summary>
    private static Rect CalculateContentBounds(
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        int offsetX,
        int offsetY,
        float nodeWidth,
        float nodeHeight,
        Vector4 effectPadding
    )
    {
        // Start with the main content area
        int contentX = offsetX;
        int contentY = offsetY;
        int contentWidth = (int)nodeWidth;
        int contentHeight = (int)nodeHeight;

        // Expand bounds to include any visible pixels (like drop shadows)
        // Use directional padding to determine initial bounds
        int minX = Mathf.Max(0, contentX - (int)effectPadding.x);
        int maxX = Mathf.Min(textureWidth, contentX + contentWidth + (int)effectPadding.y);
        int minY = Mathf.Max(0, contentY - (int)effectPadding.z);
        int maxY = Mathf.Min(textureHeight, contentY + contentHeight + (int)effectPadding.w);

        // Scan for non-transparent pixels to find actual bounds
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int index = y * textureWidth + x;
                if (index < pixels.Length && pixels[index].a > 0.01f) // Non-transparent pixel
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x + 1);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }

        // Ensure bounds are within texture
        minX = Mathf.Clamp(minX, 0, textureWidth);
        minY = Mathf.Clamp(minY, 0, textureHeight);
        maxX = Mathf.Clamp(maxX, minX + 1, textureWidth);
        maxY = Mathf.Clamp(maxY, minY + 1, textureHeight);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Renders drop shadows for the node
    /// </summary>
    private static void RenderDropShadows(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY
    )
    {
        JArray effects = nodeData["effects"] as JArray;
        if (effects == null || effects.Count == 0)
            return;

        foreach (JObject effect in effects)
        {
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
                continue;

            string effectType = effect["type"]?.ToString();
            if (effectType != "DROP_SHADOW")
                continue;

            // Get shadow properties
            JObject offset = effect["offset"] as JObject;
            float offsetX_shadow = offset?["x"]?.ToObject<float>() ?? 0f;
            float offsetY_shadow = -(offset?["y"]?.ToObject<float>() ?? 0f);
            float radius = effect["radius"]?.ToObject<float>() ?? 0f;
            JObject color = effect["color"] as JObject;
            float opacity = effect["opacity"]?.ToObject<float>() ?? 1f;

            if (color != null)
            {
                float r = color["r"]?.ToObject<float>() ?? 0f;
                float g = color["g"]?.ToObject<float>() ?? 0f;
                float b = color["b"]?.ToObject<float>() ?? 0f;
                float a = color["a"]?.ToObject<float>() ?? 1f;
                Color shadowColor = new Color(r, g, b, a * opacity);

                // Create shadow mask first
                bool[] shadowMask = CreateShadowMask(nodeData, nodeWidth, nodeHeight);

                // Render shadow with offset and blur
                RenderShadowWithMask(
                    shadowMask,
                    shadowColor,
                    pixels,
                    textureWidth,
                    textureHeight,
                    nodeWidth,
                    nodeHeight,
                    offsetX,
                    offsetY,
                    offsetX_shadow,
                    offsetY_shadow,
                    radius
                );
            }
        }
    }

    /// <summary>
    /// Creates a mask for the shadow based on the shape
    /// </summary>
    private static bool[] CreateShadowMask(JObject nodeData, float nodeWidth, float nodeHeight)
    {
        bool[] mask = new bool[(int)(nodeWidth * nodeHeight)];
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        switch (nodeType)
        {
            case "RECTANGLE":
            case "ROUNDED_RECTANGLE":
            case "FRAME":
                CreateRectangleMask(nodeData, mask, (int)nodeWidth, (int)nodeHeight);
                break;
            case "ELLIPSE":
                CreateEllipseMask(nodeData, mask, (int)nodeWidth, (int)nodeHeight);
                break;
            default:
                CreateRectangleMask(nodeData, mask, (int)nodeWidth, (int)nodeHeight);
                break;
        }

        return mask;
    }

    /// <summary>
    /// Creates mask for rectangle shapes
    /// </summary>
    private static bool[] CreateRectangleMask(JObject nodeData, int width, int height)
    {
        bool[] mask = new bool[width * height];
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
        bool hasCornerRadius = cornerRadius > 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool insideShape = true;

                if (hasCornerRadius)
                {
                    insideShape = IsInsideRoundedRect(x, y, width, height, cornerRadius);
                }

                int index = y * width + x;
                mask[index] = insideShape;
            }
        }

        return mask;
    }

    /// <summary>
    /// Creates mask for rectangle shapes (legacy method for shadow mask)
    /// </summary>
    private static void CreateRectangleMask(JObject nodeData, bool[] mask, int width, int height)
    {
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
        bool hasCornerRadius = cornerRadius > 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool insideShape = true;

                if (hasCornerRadius)
                {
                    insideShape = IsInsideRoundedRect(x, y, width, height, cornerRadius);
                }

                int index = y * width + x;
                mask[index] = insideShape;
            }
        }
    }

    /// <summary>
    /// Creates mask for ellipse shapes
    /// </summary>
    private static bool[] CreateEllipseMask(JObject nodeData, int width, int height)
    {
        bool[] mask = new bool[width * height];
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distance = dx * dx + dy * dy;

                int index = y * width + x;
                mask[index] = distance <= 1.0f;
            }
        }

        return mask;
    }

    /// <summary>
    /// Creates mask for ellipse shapes (legacy method for shadow mask)
    /// </summary>
    private static void CreateEllipseMask(JObject nodeData, bool[] mask, int width, int height)
    {
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distance = dx * dx + dy * dy;

                int index = y * width + x;
                mask[index] = distance <= 1.0f;
            }
        }
    }

    /// <summary>
    /// Renders shadow using the mask with offset and blur
    /// </summary>
    private static void RenderShadowWithMask(
        bool[] shadowMask,
        Color shadowColor,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY,
        float shadowOffsetX,
        float shadowOffsetY,
        float blurRadius
    )
    {
        int maskWidth = (int)nodeWidth;
        int maskHeight = (int)nodeHeight;

        // Calculate shadow position
        int shadowX = offsetX + (int)shadowOffsetX;
        int shadowY = offsetY + (int)shadowOffsetY;

        // Simple blur implementation
        int blurPixels = Mathf.Max(1, (int)blurRadius);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                // Check if this pixel should have shadow
                bool hasShadow = false;
                float shadowIntensity = 0f;

                // Sample the mask with blur
                for (int dy = -blurPixels; dy <= blurPixels; dy++)
                {
                    for (int dx = -blurPixels; dx <= blurPixels; dx++)
                    {
                        int maskX = x - shadowX + dx;
                        int maskY = y - shadowY + dy;

                        if (maskX >= 0 && maskX < maskWidth && maskY >= 0 && maskY < maskHeight)
                        {
                            int maskIndex = maskY * maskWidth + maskX;
                            if (shadowMask[maskIndex])
                            {
                                // Calculate distance for blur falloff
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                if (distance <= blurPixels)
                                {
                                    float intensity = 1f - (distance / blurPixels);
                                    shadowIntensity = Mathf.Max(shadowIntensity, intensity);
                                    hasShadow = true;
                                }
                            }
                        }
                    }
                }

                if (hasShadow)
                {
                    int index = y * textureWidth + x;
                    Color finalShadowColor = shadowColor;
                    finalShadowColor.a *= shadowIntensity;

                    // Alpha blend with existing pixel
                    Color existing = pixels[index];
                    pixels[index] = Color.Lerp(existing, finalShadowColor, finalShadowColor.a);
                }
            }
        }
    }

    /// <summary>
    /// Renders a rectangle/frame directly to pixels
    /// </summary>
    private static void RenderRectangle(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        // Check if node has image fills
        if (HasImageFill(nodeData))
        {
            // Create shape mask for image fill
            bool[] shapeMask = CreateRectangleMask(nodeData, (int)nodeWidth, (int)nodeHeight);

            // Get image fill and render it
            JObject imageFill = GetImageFill(nodeData);
            if (imageFill != null)
            {
                RenderImageFill(
                    imageFill,
                    pixels,
                    textureWidth,
                    textureHeight,
                    nodeWidth,
                    nodeHeight,
                    offsetX,
                    offsetY,
                    imageData,
                    shapeMask,
                    mainNodeId
                );
            }
        }
        else
        {
            // Get fill color for solid fill
            Color fillColor = GetFillColor(nodeData);

            // Get corner radius
            float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
            bool hasCornerRadius = cornerRadius > 0;

            // Fill the shape
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // Check if pixel is within the shape bounds (with offset)
                    if (
                        x < offsetX
                        || x >= offsetX + nodeWidth
                        || y < offsetY
                        || y >= offsetY + nodeHeight
                    )
                        continue;

                    bool insideShape = true;

                    if (hasCornerRadius)
                    {
                        // Check if pixel is inside rounded rectangle (relative to shape bounds)
                        insideShape = IsInsideRoundedRect(
                            x - offsetX,
                            y - offsetY,
                            (int)nodeWidth,
                            (int)nodeHeight,
                            cornerRadius
                        );
                    }

                    if (insideShape)
                    {
                        int index = y * textureWidth + x;
                        pixels[index] = fillColor;
                    }
                }
            }
        }

        // Apply stroke if present
        ApplyStroke(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            nodeWidth,
            nodeHeight,
            offsetX,
            offsetY
        );
    }

    /// <summary>
    /// Renders an ellipse directly to pixels
    /// </summary>
    private static void RenderEllipse(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        // Check if node has image fills
        if (HasImageFill(nodeData))
        {
            // Create shape mask for image fill
            bool[] shapeMask = CreateEllipseMask(nodeData, (int)nodeWidth, (int)nodeHeight);

            // Get image fill and render it
            JObject imageFill = GetImageFill(nodeData);
            if (imageFill != null)
            {
                RenderImageFill(
                    imageFill,
                    pixels,
                    textureWidth,
                    textureHeight,
                    nodeWidth,
                    nodeHeight,
                    offsetX,
                    offsetY,
                    imageData,
                    shapeMask,
                    mainNodeId
                );
            }
        }
        else
        {
            // Get fill color for solid fill
            Color fillColor = GetFillColor(nodeData);

            float centerX = offsetX + nodeWidth * 0.5f;
            float centerY = offsetY + nodeHeight * 0.5f;
            float radiusX = nodeWidth * 0.5f;
            float radiusY = nodeHeight * 0.5f;

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    float dx = (x - centerX) / radiusX;
                    float dy = (y - centerY) / radiusY;
                    float distance = dx * dx + dy * dy;

                    if (distance <= 1.0f)
                    {
                        int index = y * textureWidth + x;
                        pixels[index] = fillColor;
                    }
                }
            }
        }

        ApplyStroke(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            nodeWidth,
            nodeHeight,
            offsetX,
            offsetY
        );
    }

    /// <summary>
    /// Checks if a pixel is inside a rounded rectangle
    /// </summary>
    private static bool IsInsideRoundedRect(int x, int y, int width, int height, float radius)
    {
        // Check if in corner regions
        if (x < radius && y < radius)
        {
            // Top-left corner
            float dx = radius - x;
            float dy = radius - y;
            return (dx * dx + dy * dy) <= (radius * radius);
        }
        else if (x >= width - radius && y < radius)
        {
            // Top-right corner
            float dx = x - (width - radius);
            float dy = radius - y;
            return (dx * dx + dy * dy) <= (radius * radius);
        }
        else if (x < radius && y >= height - radius)
        {
            // Bottom-left corner
            float dx = radius - x;
            float dy = y - (height - radius);
            return (dx * dx + dy * dy) <= (radius * radius);
        }
        else if (x >= width - radius && y >= height - radius)
        {
            // Bottom-right corner
            float dx = x - (width - radius);
            float dy = y - (height - radius);
            return (dx * dx + dy * dy) <= (radius * radius);
        }

        // Not in corner region, so inside the rectangle
        return true;
    }

    /// <summary>
    /// Checks if the node has image fills
    /// </summary>
    private static bool HasImageFill(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
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
        }
        return false;
    }

    /// <summary>
    /// Gets the first image fill from Figma node data
    /// </summary>
    private static JObject GetImageFill(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
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
        }
        return null;
    }

    /// <summary>
    /// Loads image texture from base64 data
    /// </summary>
    private static Texture2D LoadImageFromBase64(string base64Data)
    {
        try
        {
            if (string.IsNullOrEmpty(base64Data))
            {
                Debug.LogError("Base64 data is null or empty");
                return null;
            }

            byte[] imageData = Convert.FromBase64String(base64Data);
            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogError("Failed to convert base64 to byte array");
                return null;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "GeneratedFromBase64";

            // Load image data - this will resize the texture automatically
            // Set markNonReadable = false to ensure texture is readable
            bool success = texture.LoadImage(imageData, false);

            if (!success)
            {
                Debug.LogError("Failed to load image data into texture");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            // Ensure texture is readable
            texture.Apply();

            // Verify texture is readable
            if (!texture.isReadable)
            {
                Debug.LogError("Texture is not readable after LoadImage and Apply");
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            Debug.Log(
                $"Successfully loaded image from base64: {texture.width}x{texture.height} (Readable: {texture.isReadable})"
            );
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading image from base64: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads image from URL and returns as Texture2D
    /// </summary>
    public static Texture2D DownloadImageFromUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning("Image URL is null or empty");
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
                byte[] imageData = client.DownloadData(imageUrl);
                if (imageData == null || imageData.Length == 0)
                {
                    Debug.LogError($"Downloaded image data is null or empty from: {imageUrl}");
                    return null;
                }

                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.name = "DownloadedFromUrl";

                // Load image data - set markNonReadable = false to ensure texture is readable
                bool success = texture.LoadImage(imageData, false);

                if (!success)
                {
                    Debug.LogError($"Failed to load image data into texture from: {imageUrl}");
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }

                // Ensure texture is readable
                texture.Apply();

                // Verify texture is readable
                if (!texture.isReadable)
                {
                    Debug.LogError(
                        $"Texture is not readable after LoadImage and Apply from: {imageUrl}"
                    );
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }

                // Cache the downloaded image
                _downloadedImageCache[imageUrl] = texture;

                Debug.Log(
                    $"Successfully downloaded image from: {imageUrl} ({texture.width}x{texture.height}) (Readable: {texture.isReadable})"
                );
                return texture;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error downloading image from URL {imageUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads image from URL asynchronously using coroutine and saves to Resources
    /// </summary>
    public static IEnumerator DownloadImageFromUrlAsync(
        string imageUrl,
        System.Action<Texture2D> onComplete,
        int maxTextureSize = 2048,
        string nodeId = null,
        string imageName = null
    )
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning("Image URL is null or empty");
            onComplete?.Invoke(null);
            yield break;
        }

        // Check cache first
        if (_downloadedImageCache.TryGetValue(imageUrl, out Texture2D cachedTexture))
        {
            onComplete?.Invoke(cachedTexture);
            yield break;
        }

        // Try to get higher resolution image by modifying URL parameters
        string highResUrl = GetHighResolutionImageUrl(imageUrl, maxTextureSize);

        using (
            UnityEngine.Networking.UnityWebRequest request =
                UnityEngine.Networking.UnityWebRequestTexture.GetTexture(highResUrl)
        )
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(
                    request
                );

                if (texture != null)
                {
                    // Ensure texture is readable
                    texture.Apply();

                    // Verify texture is readable
                    if (!texture.isReadable)
                    {
                        Debug.LogError(
                            $"Async downloaded texture is not readable from: {imageUrl}"
                        );
                        UnityEngine.Object.DestroyImmediate(texture);
                        onComplete?.Invoke(null);
                        yield break;
                    }

                    // Check if we need to upscale the texture
                    if (texture.width < maxTextureSize && texture.height < maxTextureSize)
                    {
                        Texture2D upscaledTexture = UpscaleTexture(texture, maxTextureSize);
                        if (upscaledTexture != null)
                        {
                            UnityEngine.Object.DestroyImmediate(texture);
                            texture = upscaledTexture;
                        }
                    }

                    // Save image to Resources if nodeId and imageName are provided
                    if (!string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(imageName))
                    {
                        SaveImageToResources(texture, imageName, nodeId);
                    }

                    // Cache the downloaded image
                    _downloadedImageCache[imageUrl] = texture;

                    Debug.Log(
                        $"Successfully downloaded image from: {imageUrl} ({texture.width}x{texture.height}) (Readable: {texture.isReadable})"
                    );
                    onComplete?.Invoke(texture);
                }
                else
                {
                    Debug.LogError($"Downloaded texture is null from: {imageUrl}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"Error downloading image from URL {imageUrl}: {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Saves sprite to Resources folder
    /// </summary>
    private static void SaveSpriteToResources(Sprite sprite, string spriteName, string nodeId)
    {
        if (
            sprite == null
            || sprite.texture == null
            || string.IsNullOrEmpty(spriteName)
            || string.IsNullOrEmpty(nodeId)
        )
            return;

        try
        {
            // Sanitize names
            string sanitizedSpriteName = spriteName.SanitizeFileName();
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = System.IO.Path.Combine(
                Application.dataPath,
                "Resources",
                Constant.SAVE_IMAGE_FOLDER,
                sanitizedNodeId
            );

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            // Create file path
            string fileName = $"{sanitizedSpriteName}.png";
            string filePath = System.IO.Path.Combine(folderPath, fileName);

            // Crop texture to sprite bounds to remove padding
            Texture2D croppedTexture = CropTextureToSpriteBounds(sprite);
            if (croppedTexture == null)
            {
                Debug.LogWarning($"Failed to crop sprite {spriteName}");
                return;
            }

            // Encode cropped texture to PNG
            byte[] pngData = croppedTexture.EncodeToPNG();
            if (pngData != null && pngData.Length > 0)
            {
                System.IO.File.WriteAllBytes(filePath, pngData);

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif

                Debug.Log($"✓ Saved sprite to Resources: {fileName} at {filePath}");
            }
            else
            {
                Debug.LogWarning($"Failed to encode sprite {spriteName} to PNG");
            }

            // Clean up cropped texture
            UnityEngine.Object.DestroyImmediate(croppedTexture);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving sprite {spriteName} to Resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves image texture to Resources folder
    /// </summary>
    private static void SaveImageToResources(Texture2D texture, string imageName, string nodeId)
    {
        if (texture == null || string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
            return;

        try
        {
            // Sanitize names
            string sanitizedImageName = imageName.SanitizeFileName();
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = System.IO.Path.Combine(
                Application.dataPath,
                "Resources",
                Constant.SAVE_IMAGE_FOLDER,
                sanitizedNodeId
            );

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            // Create file path
            string fileName = $"{sanitizedImageName}.png";
            string filePath = System.IO.Path.Combine(folderPath, fileName);

            // Encode texture to PNG
            byte[] pngData = texture.EncodeToPNG();
            if (pngData != null && pngData.Length > 0)
            {
                System.IO.File.WriteAllBytes(filePath, pngData);

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif

                Debug.Log($"✓ Saved image to Resources: {fileName} at {filePath}");
            }
            else
            {
                Debug.LogWarning($"Failed to encode image {imageName} to PNG");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving image {imageName} to Resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Crops texture to sprite bounds to remove padding
    /// </summary>
    private static Texture2D CropTextureToSpriteBounds(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return null;

        try
        {
            Texture2D originalTexture = sprite.texture;
            Rect spriteRect = sprite.textureRect;

            // Create new texture with sprite dimensions
            Texture2D croppedTexture = new Texture2D(
                (int)spriteRect.width,
                (int)spriteRect.height,
                originalTexture.format,
                false
            );

            // Get pixels from sprite region
            Color[] pixels = originalTexture.GetPixels(
                (int)spriteRect.x,
                (int)spriteRect.y,
                (int)spriteRect.width,
                (int)spriteRect.height
            );

            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            return croppedTexture;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error cropping texture to sprite bounds: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads sprite from Resources folder
    /// </summary>
    public static Sprite LoadSpriteFromResources(string imageName, string nodeId)
    {
        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
            return null;

        try
        {
            // Sanitize names
            string sanitizedImageName = imageName.SanitizeFileName();
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create resource path
            string resourcePath =
                $"{Constant.SAVE_IMAGE_FOLDER}/{sanitizedNodeId}/{sanitizedImageName}";

            // Load sprite from Resources
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                Debug.Log($"✓ Loaded sprite from Resources: {resourcePath}");
                return sprite;
            }
            else
            {
                Debug.LogWarning($"Sprite not found in Resources: {resourcePath}");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error loading sprite {imageName} from Resources: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads sprite from Resources folder using main nodeId (for instances)
    /// </summary>
    public static Sprite LoadSpriteFromResourcesWithMainNodeId(string imageName, string instanceNodeId, string mainNodeId)
    {
        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(instanceNodeId))
            return null;

        // First try with instance nodeId
        Sprite sprite = LoadSpriteFromResources(imageName, instanceNodeId);
        if (sprite != null)
        {
            return sprite;
        }

        // If not found and mainNodeId is provided, try with main nodeId
        if (!string.IsNullOrEmpty(mainNodeId) && mainNodeId != instanceNodeId)
        {
            Debug.Log($"Sprite not found with instance nodeId {instanceNodeId}, trying with main nodeId {mainNodeId}");
            sprite = LoadSpriteFromResources(imageName, mainNodeId);
            if (sprite != null)
            {
                Debug.Log($"✓ Loaded sprite from Resources using main nodeId: {mainNodeId}");
                return sprite;
            }
        }

        return null;
    }

    /// <summary>
    /// Clears the downloaded image cache
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
        Debug.Log("Image cache cleared");
    }

    /// <summary>
    /// Modifies image URL to request higher resolution
    /// </summary>
    private static string GetHighResolutionImageUrl(string originalUrl, int maxSize)
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        // For Figma images, try to modify scale parameter
        if (originalUrl.Contains("figma.com") || originalUrl.Contains("figma-alpha"))
        {
            // Calculate scale based on desired max size
            float scale = Mathf.Clamp(maxSize / 512f, 1f, 4f); // Figma typically serves 512px base

            if (originalUrl.Contains("scale="))
            {
                // Replace existing scale parameter
                return System.Text.RegularExpressions.Regex.Replace(
                    originalUrl,
                    @"scale=\d+(\.\d+)?",
                    $"scale={scale}"
                );
            }
            else
            {
                // Add scale parameter
                string separator = originalUrl.Contains("?") ? "&" : "?";
                return $"{originalUrl}{separator}scale={scale}";
            }
        }

        return originalUrl;
    }

    /// <summary>
    /// Upscales a texture to higher resolution using bilinear filtering
    /// </summary>
    private static Texture2D UpscaleTexture(Texture2D originalTexture, int maxSize)
    {
        if (originalTexture == null || !originalTexture.isReadable)
            return null;

        try
        {
            int originalWidth = originalTexture.width;
            int originalHeight = originalTexture.height;

            // Calculate new size (maintain aspect ratio)
            float aspectRatio = (float)originalWidth / originalHeight;
            int newWidth,
                newHeight;

            if (originalWidth > originalHeight)
            {
                newWidth = Mathf.Min(maxSize, originalWidth * 2);
                newHeight = Mathf.RoundToInt(newWidth / aspectRatio);
            }
            else
            {
                newHeight = Mathf.Min(maxSize, originalHeight * 2);
                newWidth = Mathf.RoundToInt(newHeight * aspectRatio);
            }

            // Create new texture
            Texture2D upscaledTexture = new Texture2D(
                newWidth,
                newHeight,
                TextureFormat.RGBA32,
                false
            );
            upscaledTexture.name = $"Upscaled_{originalTexture.name}";

            // Get original pixels
            Color[] originalPixels = originalTexture.GetPixels();
            Color[] newPixels = new Color[newWidth * newHeight];

            // Upscale using bilinear interpolation
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float u = (float)x / (newWidth - 1);
                    float v = (float)y / (newHeight - 1);

                    float origX = u * (originalWidth - 1);
                    float origY = v * (originalHeight - 1);

                    int x1 = Mathf.FloorToInt(origX);
                    int y1 = Mathf.FloorToInt(origY);
                    int x2 = Mathf.Min(x1 + 1, originalWidth - 1);
                    int y2 = Mathf.Min(y1 + 1, originalHeight - 1);

                    float fx = origX - x1;
                    float fy = origY - y1;

                    Color c11 = originalPixels[y1 * originalWidth + x1];
                    Color c12 = originalPixels[y2 * originalWidth + x1];
                    Color c21 = originalPixels[y1 * originalWidth + x2];
                    Color c22 = originalPixels[y2 * originalWidth + x2];

                    Color c1 = Color.Lerp(c11, c21, fx);
                    Color c2 = Color.Lerp(c12, c22, fx);
                    Color finalColor = Color.Lerp(c1, c2, fy);

                    newPixels[y * newWidth + x] = finalColor;
                }
            }

            upscaledTexture.SetPixels(newPixels);
            upscaledTexture.Apply();

            Debug.Log(
                $"Upscaled texture from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}"
            );
            return upscaledTexture;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error upscaling texture: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts byte array to base64 string for DirectSpriteGenerator
    /// </summary>
    public static string ConvertImageDataToBase64(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return null;

        return Convert.ToBase64String(imageData);
    }

    /// <summary>
    /// Converts FigmaApi image data dictionary to DirectSpriteGenerator format
    /// </summary>
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

    /// <summary>
    /// Renders image fill to pixels with proper scaling and positioning
    /// </summary>
    private static void RenderImageFill(
        JObject imageFill,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData,
        bool[] shapeMask = null,
        string mainNodeId = null
    )
    {
        // Get image data from fill
        string imageRef = imageFill["imageRef"]?.ToString();
        string imageUrl = imageFill["imageUrl"]?.ToString();

        Debug.Log($"RenderImageFill: imageRef={imageRef}, imageUrl={imageUrl}");

        if (string.IsNullOrEmpty(imageRef) && string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning("Image fill missing both imageRef and imageUrl");
            return;
        }

        Texture2D imageTexture = null;

        // First, try to load from Resources using imageRef as imageName
        if (!string.IsNullOrEmpty(imageRef))
        {
            Debug.Log($"RenderImageFill: Trying to load from Resources using imageRef: {imageRef}");
            
            // Try to load sprite from Resources first
            Sprite savedSprite = LoadSpriteFromResourcesWithMainNodeId(imageRef, imageRef, mainNodeId);
            if (savedSprite != null && savedSprite.texture != null)
            {
                Debug.Log($"RenderImageFill: Successfully loaded sprite from Resources: {imageRef}");
                imageTexture = savedSprite.texture;
            }
        }

        // If not found in Resources, try to get image from URL
        if (imageTexture == null && !string.IsNullOrEmpty(imageUrl))
        {
            Debug.Log($"RenderImageFill: Downloading image from URL: {imageUrl}");
            imageTexture = DownloadImageFromUrl(imageUrl);
        }
        // Fallback to base64 data from dictionary
        else if (
            imageTexture == null
            && !string.IsNullOrEmpty(imageRef)
            && imageData != null
            && imageData.TryGetValue(imageRef, out string base64Data)
        )
        {
            Debug.Log($"RenderImageFill: Loading image from base64 data for ref: {imageRef}");
            imageTexture = LoadImageFromBase64(base64Data);
        }
        else if (imageTexture == null)
        {
            Debug.LogWarning($"Image data not found for ref: {imageRef} and no URL provided");
            if (imageData != null)
            {
                Debug.LogWarning($"Available imageRefs: {string.Join(", ", imageData.Keys)}");
            }
            return;
        }

        if (imageTexture == null)
        {
            Debug.LogError($"Failed to load image for ref: {imageRef} or URL: {imageUrl}");
            return;
        }

        // Get fill properties
        float opacity = imageFill["opacity"]?.ToObject<float>() ?? 1f;
        string scaleMode = imageFill["scaleMode"]?.ToString() ?? "FILL";

        // Get image transform properties
        JObject imageTransform = imageFill["imageTransform"] as JObject;
        float scaleX = imageTransform?["scaleX"]?.ToObject<float>() ?? 1f;
        float scaleY = imageTransform?["scaleY"]?.ToObject<float>() ?? 1f;
        float rotation = imageTransform?["rotation"]?.ToObject<float>() ?? 0f;
        float translationX = imageTransform?["translationX"]?.ToObject<float>() ?? 0f;
        float translationY = imageTransform?["translationY"]?.ToObject<float>() ?? 0f;

        // Render image to pixels
        RenderImageToPixels(
            imageTexture,
            pixels,
            textureWidth,
            textureHeight,
            nodeWidth,
            nodeHeight,
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

        // Don't destroy cached textures, only destroy if it's not in cache
        if (!_downloadedImageCache.ContainsValue(imageTexture))
        {
            UnityEngine.Object.DestroyImmediate(imageTexture);
        }
    }

    /// <summary>
    /// Renders image texture to pixel array with scaling and positioning
    /// </summary>
    private static void RenderImageToPixels(
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
            Debug.LogError("RenderImageToPixels: imageTexture is null");
            return;
        }

        if (!imageTexture.isReadable)
        {
            Debug.LogError(
                $"RenderImageToPixels: imageTexture '{imageTexture.name}' is not readable"
            );
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
                $"RenderImageToPixels: Rendering {imageWidth}x{imageHeight} image to {textureWidth}x{textureHeight} texture"
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RenderImageToPixels: Failed to get pixels from texture: {ex.Message}");
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
                // Scale to fit within the node (maintain aspect ratio)
                float scale = Mathf.Min(nodeWidth / imageWidth, nodeHeight / imageHeight);
                finalScaleX = finalScaleY = scale;
                break;
            case "CROP":
                // Scale to fill and crop if necessary
                float cropScale = Mathf.Max(nodeWidth / imageWidth, nodeHeight / imageHeight);
                finalScaleX = finalScaleY = cropScale;
                break;
            case "TILE":
                // Tile the image
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
                    int imageIndex = imagePixelY * imageWidth + imagePixelX;

                    if (imageIndex >= 0 && imageIndex < imagePixels.Length)
                    {
                        Color imageColor = imagePixels[imageIndex];
                        imageColor.a *= opacity;

                        int pixelIndex = y * textureWidth + x;
                        Color existingColor = pixels[pixelIndex];
                        pixels[pixelIndex] = Color.Lerp(existingColor, imageColor, imageColor.a);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets fill color from Figma node data
    /// </summary>
    private static Color GetFillColor(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            JObject firstFill = fills[0] as JObject;
            if (firstFill != null)
            {
                bool visible = firstFill["visible"]?.ToObject<bool>() ?? true;
                if (visible)
                {
                    string fillType = firstFill["type"]?.ToString();
                    if (fillType == "SOLID")
                    {
                        JObject color = firstFill["color"] as JObject;
                        float opacity = firstFill["opacity"]?.ToObject<float>() ?? 1f;

                        if (color != null)
                        {
                            float r = color["r"]?.ToObject<float>() ?? 0f;
                            float g = color["g"]?.ToObject<float>() ?? 0f;
                            float b = color["b"]?.ToObject<float>() ?? 0f;
                            float a = color["a"]?.ToObject<float>() ?? 1f;
                            return new Color(r, g, b, a * opacity);
                        }
                    }
                }
            }
        }

        return Color.clear;
    }

    /// <summary>
    /// Applies stroke to the shape
    /// </summary>
    private static void ApplyStroke(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY
    )
    {
        JArray strokes = nodeData["strokes"] as JArray;
        float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;

        if (strokes != null && strokes.Count > 0 && strokeWeight > 0)
        {
            JObject firstStroke = strokes[0] as JObject;
            if (firstStroke != null)
            {
                bool visible = firstStroke["visible"]?.ToObject<bool>() ?? true;
                if (visible && firstStroke["type"]?.ToString() == "SOLID")
                {
                    JObject color = firstStroke["color"] as JObject;
                    float opacity = firstStroke["opacity"]?.ToObject<float>() ?? 1f;

                    if (color != null)
                    {
                        float r = color["r"]?.ToObject<float>() ?? 0f;
                        float g = color["g"]?.ToObject<float>() ?? 0f;
                        float b = color["b"]?.ToObject<float>() ?? 0f;
                        float a = color["a"]?.ToObject<float>() ?? 1f;
                        Color strokeColor = new Color(r, g, b, a * opacity);

                        int strokePixels = (int)strokeWeight;

                        // Simple stroke rendering on edges (relative to shape bounds)
                        for (int y = offsetY; y < offsetY + nodeHeight; y++)
                        {
                            for (int x = offsetX; x < offsetX + nodeWidth; x++)
                            {
                                if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight)
                                    continue;

                                bool isEdge =
                                    x < offsetX + strokePixels
                                    || x >= offsetX + nodeWidth - strokePixels
                                    || y < offsetY + strokePixels
                                    || y >= offsetY + nodeHeight - strokePixels;

                                if (isEdge)
                                {
                                    int index = y * textureWidth + x;
                                    // Alpha blend with existing pixel
                                    Color existing = pixels[index];
                                    pixels[index] = Color.Lerp(
                                        existing,
                                        strokeColor,
                                        strokeColor.a
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

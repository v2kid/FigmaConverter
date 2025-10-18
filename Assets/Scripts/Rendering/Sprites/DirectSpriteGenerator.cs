using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class DirectSpriteGenerator
{
    private const int DEFAULT_TEXTURE_SIZE = 512;
    private const float PIXELS_PER_UNIT = 100f;

    /// <summary>
    /// Generates sprite directly from Figma node without SVG intermediate
    /// </summary>
    public static Sprite GenerateSpriteFromNodeDirect(JObject nodeData, float width, float height)
    {
        try
        {
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
                        offsetY
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
                        offsetY
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
                        offsetY
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

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating sprite directly: {ex.Message}");
            return null;
        }
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
    /// Calculates total padding needed for effects (for backward compatibility)
    /// </summary>
 

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
        int offsetY
    )
    {
        // Get fill color
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
        int offsetY
    )
    {
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

using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// DirectSpriteGenerator: Generates Unity sprites directly from Figma node properties
/// WITHOUT using SVG as intermediate format
/// 
/// Process: Figma Node → Direct Rasterization → Texture2D → Sprite
/// 
/// This is faster than NodeSpriteGenerator but harder to debug since there's no SVG output.
/// Use this for production, use NodeSpriteGenerator (with SVG) for development/debugging.
/// </summary>
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
            // Calculate texture size based on dimensions
            int textureWidth = Mathf.NextPowerOfTwo((int)width);
            int textureHeight = Mathf.NextPowerOfTwo((int)height);
            textureWidth = Mathf.Max(textureWidth, 64);
            textureHeight = Mathf.Max(textureHeight, 64);
            textureWidth = Mathf.Min(textureWidth, 2048);
            textureHeight = Mathf.Min(textureHeight, 2048);

            // Create texture
            Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[textureWidth * textureHeight];
            
            // Initialize to transparent
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            // Get node type and render accordingly
            string nodeType = nodeData["type"]?.ToString()?.ToUpper();

            switch (nodeType)
            {
                case "RECTANGLE":
                case "ROUNDED_RECTANGLE":
                case "FRAME":
                    RenderRectangle(nodeData, pixels, textureWidth, textureHeight, width, height);
                    break;

                case "ELLIPSE":
                    RenderEllipse(nodeData, pixels, textureWidth, textureHeight, width, height);
                    break;

                default:
                    RenderRectangle(nodeData, pixels, textureWidth, textureHeight, width, height);
                    break;
            }

            // Apply pixels to texture
            texture.SetPixels(pixels);
            texture.Apply();

            // Create sprite
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, textureWidth, textureHeight),
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
    /// Renders a rectangle/frame directly to pixels
    /// </summary>
    private static void RenderRectangle(
        JObject nodeData, 
        Color[] pixels, 
        int textureWidth, 
        int textureHeight,
        float nodeWidth,
        float nodeHeight)
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
                bool insideShape = true;

                if (hasCornerRadius)
                {
                    // Check if pixel is inside rounded rectangle
                    insideShape = IsInsideRoundedRect(
                        x, y, 
                        textureWidth, textureHeight, 
                        cornerRadius * textureWidth / nodeWidth
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
        ApplyStroke(nodeData, pixels, textureWidth, textureHeight, nodeWidth, nodeHeight);
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
        float nodeHeight)
    {
        Color fillColor = GetFillColor(nodeData);
        
        float centerX = textureWidth * 0.5f;
        float centerY = textureHeight * 0.5f;
        float radiusX = textureWidth * 0.5f;
        float radiusY = textureHeight * 0.5f;

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

        ApplyStroke(nodeData, pixels, textureWidth, textureHeight, nodeWidth, nodeHeight);
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
        float nodeHeight)
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

                        int strokePixels = (int)(strokeWeight * textureWidth / nodeWidth);

                        // Simple stroke rendering on edges
                        for (int y = 0; y < textureHeight; y++)
                        {
                            for (int x = 0; x < textureWidth; x++)
                            {
                                bool isEdge = x < strokePixels || 
                                            x >= textureWidth - strokePixels ||
                                            y < strokePixels || 
                                            y >= textureHeight - strokePixels;

                                if (isEdge)
                                {
                                    int index = y * textureWidth + x;
                                    // Alpha blend with existing pixel
                                    Color existing = pixels[index];
                                    pixels[index] = Color.Lerp(existing, strokeColor, strokeColor.a);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
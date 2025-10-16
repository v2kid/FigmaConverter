using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FigmaShapeRenderer
{
    public static void RenderRectangle(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight)
    {
        Color fillColor = FigmaFillHandler.GetSolidFillColor(nodeData);
        float cornerRadius = GetCornerRadius(nodeData);

        float scaleX = textureWidth / nodeWidth;
        float scaleY = textureHeight / nodeHeight;

        // Debug log for corner radius
        string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
        if (cornerRadius > 0)
        {
            Debug.Log($"🔵 Rendering {nodeName}: cornerRadius={cornerRadius}, " +
                     $"texture={textureWidth}x{textureHeight}, node={nodeWidth}x{nodeHeight}, " +
                     $"scaledRadius={cornerRadius * scaleX}");
        }

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                bool isInside = IsInsideRoundedRectangle(
                    x, y,
                    textureWidth, textureHeight,
                    cornerRadius * scaleX
                );

                if (isInside)
                {
                    pixels[y * textureWidth + x] = fillColor;
                }
            }
        }
    }

    public static void RenderEllipse(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight)
    {
        Color fillColor = FigmaFillHandler.GetSolidFillColor(nodeData);

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
                    pixels[y * textureWidth + x] = fillColor;
                }
            }
        }
    }

    public static string GenerateSVGRectangle(JObject nodeData, float width, float height, float x = 0, float y = 0)
    {
        float cornerRadius = GetCornerRadius(nodeData);
        string fill = FigmaFillHandler.GenerateSVGFill(nodeData);
        string stroke = FigmaStrokeHandler.GenerateSVGStroke(nodeData);
        
        string defs = string.Empty;
        string filterAttr = string.Empty;

        if (fill.Contains("||"))
        {
            string[] parts = fill.Split(new[] { "||" }, System.StringSplitOptions.None);
            fill = parts[0];
            defs += parts[1];
        }

        float effectPadding;
        string effects = FigmaEffectHandler.GenerateSVGEffects(nodeData, out effectPadding);
        if (effects.Contains("||"))
        {
            string[] parts = effects.Split(new[] { "||" }, System.StringSplitOptions.None);
            filterAttr = parts[0];
            defs += parts[1];
        }

        float opacity = nodeData["opacity"]?.ToObject<float>() ?? 1f;
        string opacityAttr = opacity < 1f ? $"opacity=\"{opacity}\"" : string.Empty;

        string rectTag;
        if (cornerRadius > 0)
        {
            rectTag = $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" " +
                     $"rx=\"{cornerRadius}\" ry=\"{cornerRadius}\" " +
                     $"{fill} {stroke} {filterAttr} {opacityAttr} />";
        }
        else
        {
            rectTag = $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" " +
                     $"{fill} {stroke} {filterAttr} {opacityAttr} />";
        }

        return defs + rectTag;
    }

    public static string GenerateSVGEllipse(JObject nodeData, float width, float height, float cx = 0, float cy = 0)
    {
        float rx = width * 0.5f;
        float ry = height * 0.5f;
        
        if (cx == 0)
            cx = rx;
        if (cy == 0)
            cy = ry;

        string fill = FigmaFillHandler.GenerateSVGFill(nodeData);
        string stroke = FigmaStrokeHandler.GenerateSVGStroke(nodeData);
        
        string defs = string.Empty;
        string filterAttr = string.Empty;

        if (fill.Contains("||"))
        {
            string[] parts = fill.Split(new[] { "||" }, System.StringSplitOptions.None);
            fill = parts[0];
            defs += parts[1];
        }

        float effectPadding;
        string effects = FigmaEffectHandler.GenerateSVGEffects(nodeData, out effectPadding);
        if (effects.Contains("||"))
        {
            string[] parts = effects.Split(new[] { "||" }, System.StringSplitOptions.None);
            filterAttr = parts[0];
            defs += parts[1];
        }

        float opacity = nodeData["opacity"]?.ToObject<float>() ?? 1f;
        string opacityAttr = opacity < 1f ? $"opacity=\"{opacity}\"" : string.Empty;

        string ellipseTag = $"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx}\" ry=\"{ry}\" " +
                           $"{fill} {stroke} {filterAttr} {opacityAttr} />";

        return defs + ellipseTag;
    }

    private static float GetCornerRadius(JObject nodeData)
    {
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
        
        JArray rectangleCornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (rectangleCornerRadii != null && rectangleCornerRadii.Count > 0)
        {
            float topLeft = rectangleCornerRadii[0]?.ToObject<float>() ?? 0f;
            string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
            Debug.Log($"🟡 {nodeName}: Using rectangleCornerRadii[0] = {topLeft}");
            return topLeft;
        }

        if (cornerRadius > 0)
        {
            string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
            Debug.Log($"🟡 {nodeName}: Using cornerRadius = {cornerRadius}");
        }

        return cornerRadius;
    }

    private static bool IsInsideRoundedRectangle(
        int x, int y,
        int width, int height,
        float cornerRadius)
    {
        if (cornerRadius <= 0)
            return true;

        bool inLeftCorner = x < cornerRadius;
        bool inRightCorner = x > width - cornerRadius;
        bool inTopCorner = y < cornerRadius;
        bool inBottomCorner = y > height - cornerRadius;

        if (inLeftCorner && inTopCorner)
        {
            float dx = x - cornerRadius;
            float dy = y - cornerRadius;
            return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
        }

        if (inRightCorner && inTopCorner)
        {
            float dx = x - (width - cornerRadius);
            float dy = y - cornerRadius;
            return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
        }

        if (inLeftCorner && inBottomCorner)
        {
            float dx = x - cornerRadius;
            float dy = y - (height - cornerRadius);
            return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
        }

        if (inRightCorner && inBottomCorner)
        {
            float dx = x - (width - cornerRadius);
            float dy = y - (height - cornerRadius);
            return (dx * dx + dy * dy) <= (cornerRadius * cornerRadius);
        }

        return true;
    }
}


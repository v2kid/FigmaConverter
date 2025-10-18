using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Handles rendering of basic shapes like rectangles, ellipses, and other geometric forms
/// Applies fills, strokes, and other visual properties to shapes
/// </summary>
public static class ShapeRenderer
{
    /// <summary>
    /// Renders a rectangle shape with fills and strokes
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the rectangle</param>
    /// <param name="height">Height of the rectangle</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="imageData">Image data for image fills</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    public static void RenderRectangle(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        if (nodeData == null || pixels == null)
            return;

        // Create mask for the rectangle
        bool[] mask = ShapeMaskGenerator.CreateRectangleMask(nodeData, (int)width, (int)height);

        // Render fills
        RenderFills(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask,
            imageData,
            mainNodeId
        );

        // Render strokes
        RenderStrokes(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask
        );
    }

    /// <summary>
    /// Renders an ellipse shape with fills and strokes
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the ellipse</param>
    /// <param name="height">Height of the ellipse</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="imageData">Image data for image fills</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    public static void RenderEllipse(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        if (nodeData == null || pixels == null)
            return;

        // Create mask for the ellipse
        bool[] mask = ShapeMaskGenerator.CreateEllipseMask(nodeData, (int)width, (int)height);

        // Render fills
        RenderFills(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask,
            imageData,
            mainNodeId
        );

        // Render strokes
        RenderStrokes(
            nodeData,
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask
        );
    }

    /// <summary>
    /// Renders fills for a shape
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    /// <param name="imageData">Image data for image fills</param>
    /// <param name="mainNodeId">Main node ID for image references</param>
    private static void RenderFills(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask,
        Dictionary<string, string> imageData = null,
        string mainNodeId = null
    )
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
        {
            // No fills specified, use default color
            RenderSolidFill(
                pixels,
                textureWidth,
                textureHeight,
                width,
                height,
                offsetX,
                offsetY,
                mask,
                SpriteGenerationConstants.DEFAULT_FILL_COLOR
            );
            return;
        }

        foreach (JObject fill in fills)
        {
            string fillType = fill["type"]?.ToString();

            switch (fillType)
            {
                case "SOLID":
                    Color solidColor = GetFillColorFromFill(fill);
                    RenderSolidFill(
                        pixels,
                        textureWidth,
                        textureHeight,
                        width,
                        height,
                        offsetX,
                        offsetY,
                        mask,
                        solidColor
                    );
                    break;

                case "IMAGE":
                    if (imageData != null)
                    {
                        RenderImageFill(
                            nodeData,
                            pixels,
                            textureWidth,
                            textureHeight,
                            width,
                            height,
                            offsetX,
                            offsetY,
                            mask,
                            imageData,
                            mainNodeId
                        );
                    }
                    break;

                case "GRADIENT_LINEAR":
                case "GRADIENT_RADIAL":
                case "GRADIENT_ANGULAR":
                case "GRADIENT_DIAMOND":
                    RenderGradientFill(
                        fill,
                        pixels,
                        textureWidth,
                        textureHeight,
                        width,
                        height,
                        offsetX,
                        offsetY,
                        mask
                    );
                    break;

                default:
                    // Fallback to solid fill
                    RenderSolidFill(
                        nodeData,
                        pixels,
                        textureWidth,
                        textureHeight,
                        width,
                        height,
                        offsetX,
                        offsetY,
                        mask
                    );
                    break;
            }
        }
    }

    /// <summary>
    /// Renders a solid color fill
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    private static void RenderSolidFill(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask
    )
    {
        Color fillColor = GetFillColor(nodeData);
        RenderSolidFill(
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask,
            fillColor
        );
    }

    /// <summary>
    /// Renders a solid color fill with specified color
    /// </summary>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    /// <param name="color">Fill color</param>
    private static void RenderSolidFill(
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask,
        Color color
    )
    {
        if (mask == null)
            return;

        int maskWidth = (int)width;
        int maskHeight = (int)height;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int pixelIndex = y * textureWidth + x;
                if (pixelIndex >= pixels.Length)
                    continue;

                // Check if pixel is within shape bounds
                int localX = x - offsetX;
                int localY = y - offsetY;

                if (localX >= 0 && localX < maskWidth && localY >= 0 && localY < maskHeight)
                {
                    int maskIndex = localY * maskWidth + localX;
                    if (maskIndex < mask.Length && mask[maskIndex])
                    {
                        pixels[pixelIndex] = color;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders an image fill
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    /// <param name="imageData">Image data</param>
    /// <param name="mainNodeId">Main node ID</param>
    private static void RenderImageFill(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask,
        Dictionary<string, string> imageData,
        string mainNodeId
    )
    {
        // This will be implemented by ImageRenderer
        // For now, fallback to solid fill
        Color fallbackColor = GetFillColor(nodeData);
        RenderSolidFill(
            pixels,
            textureWidth,
            textureHeight,
            width,
            height,
            offsetX,
            offsetY,
            mask,
            fallbackColor
        );
    }

    /// <summary>
    /// Renders a gradient fill
    /// </summary>
    /// <param name="fillData">Gradient fill data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    private static void RenderGradientFill(
        JObject fillData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask
    )
    {
        if (mask == null)
            return;

        string gradientType = fillData["type"]?.ToString();
        JArray gradientStops = fillData["gradientStops"] as JArray;

        if (gradientStops == null || gradientStops.Count == 0)
            return;

        int maskWidth = (int)width;
        int maskHeight = (int)height;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int pixelIndex = y * textureWidth + x;
                if (pixelIndex >= pixels.Length)
                    continue;

                // Check if pixel is within shape bounds
                int localX = x - offsetX;
                int localY = y - offsetY;

                if (localX >= 0 && localX < maskWidth && localY >= 0 && localY < maskHeight)
                {
                    int maskIndex = localY * maskWidth + localX;
                    if (maskIndex < mask.Length && mask[maskIndex])
                    {
                        Color gradientColor = CalculateGradientColor(
                            fillData,
                            localX,
                            localY,
                            maskWidth,
                            maskHeight
                        );
                        pixels[pixelIndex] = gradientColor;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Calculates gradient color at a specific position
    /// </summary>
    /// <param name="fillData">Gradient fill data</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <returns>Gradient color at the position</returns>
    private static Color CalculateGradientColor(
        JObject fillData,
        int x,
        int y,
        int width,
        int height
    )
    {
        string gradientType = fillData["type"]?.ToString();
        JArray gradientStops = fillData["gradientStops"] as JArray;

        if (gradientStops == null || gradientStops.Count == 0)
            return Color.white;

        float t = 0f;

        switch (gradientType)
        {
            case "GRADIENT_LINEAR":
                t = CalculateLinearGradientT(fillData, x, y, width, height);
                break;

            case "GRADIENT_RADIAL":
                t = CalculateRadialGradientT(fillData, x, y, width, height);
                break;

            default:
                // Default to linear gradient
                t = CalculateLinearGradientT(fillData, x, y, width, height);
                break;
        }

        return InterpolateGradientStops(gradientStops, t);
    }

    /// <summary>
    /// Calculates the t value for linear gradient
    /// </summary>
    /// <param name="fillData">Gradient fill data</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <returns>T value for gradient interpolation</returns>
    private static float CalculateLinearGradientT(
        JObject fillData,
        int x,
        int y,
        int width,
        int height
    )
    {
        // Get gradient transform
        JObject gradientTransform = fillData["gradientTransform"] as JObject;
        if (gradientTransform == null)
        {
            // Default horizontal gradient
            return (float)x / width;
        }

        // For now, use simple horizontal gradient
        // TODO: Implement proper gradient transform matrix
        return (float)x / width;
    }

    /// <summary>
    /// Calculates the t value for radial gradient
    /// </summary>
    /// <param name="fillData">Gradient fill data</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <returns>T value for gradient interpolation</returns>
    private static float CalculateRadialGradientT(
        JObject fillData,
        int x,
        int y,
        int width,
        int height
    )
    {
        // Calculate distance from center
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
        float maxDistance = Mathf.Sqrt(centerX * centerX + centerY * centerY);

        return Mathf.Clamp01(distance / maxDistance);
    }

    /// <summary>
    /// Interpolates between gradient stops
    /// </summary>
    /// <param name="gradientStops">Array of gradient stops</param>
    /// <param name="t">Interpolation parameter (0-1)</param>
    /// <returns>Interpolated color</returns>
    private static Color InterpolateGradientStops(JArray gradientStops, float t)
    {
        if (gradientStops.Count == 0)
            return Color.white;

        if (gradientStops.Count == 1)
        {
            return GetColorFromGradientStop(gradientStops[0] as JObject);
        }

        // Find the two stops to interpolate between
        for (int i = 0; i < gradientStops.Count - 1; i++)
        {
            JObject stop1 = gradientStops[i] as JObject;
            JObject stop2 = gradientStops[i + 1] as JObject;

            float position1 = stop1["position"]?.Value<float>() ?? 0f;
            float position2 = stop2["position"]?.Value<float>() ?? 1f;

            if (t >= position1 && t <= position2)
            {
                float localT = (t - position1) / (position2 - position1);
                Color color1 = GetColorFromGradientStop(stop1);
                Color color2 = GetColorFromGradientStop(stop2);

                return Color.Lerp(color1, color2, localT);
            }
        }

        // If t is outside the range, return the closest stop
        if (t <= 0)
            return GetColorFromGradientStop(gradientStops[0] as JObject);
        else
            return GetColorFromGradientStop(gradientStops[gradientStops.Count - 1] as JObject);
    }

    /// <summary>
    /// Gets color from a gradient stop
    /// </summary>
    /// <param name="stopData">Gradient stop data</param>
    /// <returns>Color from the stop</returns>
    private static Color GetColorFromGradientStop(JObject stopData)
    {
        if (stopData == null)
            return Color.white;

        JObject colorData = stopData["color"] as JObject;
        if (colorData == null)
            return Color.white;

        float r = colorData["r"]?.Value<float>() ?? 1f;
        float g = colorData["g"]?.Value<float>() ?? 1f;
        float b = colorData["b"]?.Value<float>() ?? 1f;
        float a = stopData["opacity"]?.Value<float>() ?? 1f;

        return new Color(r, g, b, a);
    }

    /// <summary>
    /// Renders strokes for a shape
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    private static void RenderStrokes(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask
    )
    {
        JArray strokes = nodeData["strokes"] as JArray;
        if (strokes == null || strokes.Count == 0)
            return;

        float strokeWeight =
            nodeData["strokeWeight"]?.Value<float>()
            ?? SpriteGenerationConstants.DEFAULT_STROKE_WEIGHT;
        if (strokeWeight <= 0)
            return;

        foreach (JObject stroke in strokes)
        {
            Color strokeColor = GetStrokeColor(stroke);
            ApplyStroke(
                pixels,
                textureWidth,
                textureHeight,
                width,
                height,
                offsetX,
                offsetY,
                mask,
                strokeColor,
                strokeWeight
            );
        }
    }

    /// <summary>
    /// Applies stroke to a shape
    /// </summary>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="offsetX">X offset in texture</param>
    /// <param name="offsetY">Y offset in texture</param>
    /// <param name="mask">Shape mask</param>
    /// <param name="strokeColor">Stroke color</param>
    /// <param name="strokeWeight">Stroke weight</param>
    private static void ApplyStroke(
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float width,
        float height,
        int offsetX,
        int offsetY,
        bool[] mask,
        Color strokeColor,
        float strokeWeight
    )
    {
        if (mask == null)
            return;

        int maskWidth = (int)width;
        int maskHeight = (int)height;
        int strokePixels = Mathf.CeilToInt(strokeWeight);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int pixelIndex = y * textureWidth + x;
                if (pixelIndex >= pixels.Length)
                    continue;

                // Check if pixel is within shape bounds
                int localX = x - offsetX;
                int localY = y - offsetY;

                if (localX >= 0 && localX < maskWidth && localY >= 0 && localY < maskHeight)
                {
                    int maskIndex = localY * maskWidth + localX;
                    if (maskIndex < mask.Length && mask[maskIndex])
                    {
                        // Check if this pixel is on the edge of the shape
                        if (
                            IsOnShapeEdge(localX, localY, maskWidth, maskHeight, mask, strokePixels)
                        )
                        {
                            pixels[pixelIndex] = strokeColor;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a pixel is on the edge of a shape
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Width of the shape</param>
    /// <param name="height">Height of the shape</param>
    /// <param name="mask">Shape mask</param>
    /// <param name="strokePixels">Stroke width in pixels</param>
    /// <returns>True if pixel is on the edge</returns>
    private static bool IsOnShapeEdge(
        int x,
        int y,
        int width,
        int height,
        bool[] mask,
        int strokePixels
    )
    {
        // Check if any neighboring pixel is outside the shape
        for (int dy = -strokePixels; dy <= strokePixels; dy++)
        {
            for (int dx = -strokePixels; dx <= strokePixels; dx++)
            {
                int neighborX = x + dx;
                int neighborY = y + dy;

                if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                {
                    return true; // Neighbor is outside bounds
                }

                int neighborIndex = neighborY * width + neighborX;
                if (neighborIndex < mask.Length && !mask[neighborIndex])
                {
                    return true; // Neighbor is outside the shape
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the fill color from a specific fill object
    /// </summary>
    /// <param name="fill">Fill object</param>
    /// <returns>Fill color</returns>
    private static Color GetFillColorFromFill(JObject fill)
    {
        if (fill == null)
            return SpriteGenerationConstants.DEFAULT_FILL_COLOR;

        JObject colorData = fill["color"] as JObject;
        if (colorData != null)
        {
            float r = colorData["r"]?.Value<float>() ?? 1f;
            float g = colorData["g"]?.Value<float>() ?? 1f;
            float b = colorData["b"]?.Value<float>() ?? 1f;
            float a = fill["opacity"]?.Value<float>() ?? 1f;

            return new Color(r, g, b, a);
        }

        return SpriteGenerationConstants.DEFAULT_FILL_COLOR;
    }

    /// <summary>
    /// Gets the fill color from node data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <returns>Fill color</returns>
    private static Color GetFillColor(JObject nodeData)
    {
        if (nodeData == null)
            return SpriteGenerationConstants.DEFAULT_FILL_COLOR;

        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return SpriteGenerationConstants.DEFAULT_FILL_COLOR;

        // Get the first solid fill
        foreach (JObject fill in fills)
        {
            string fillType = fill["type"]?.ToString();
            if (fillType == "SOLID")
            {
                JObject colorData = fill["color"] as JObject;
                if (colorData != null)
                {
                    float r = colorData["r"]?.Value<float>() ?? 1f;
                    float g = colorData["g"]?.Value<float>() ?? 1f;
                    float b = colorData["b"]?.Value<float>() ?? 1f;
                    float a = fill["opacity"]?.Value<float>() ?? 1f;

                    return new Color(r, g, b, a);
                }
            }
        }

        return SpriteGenerationConstants.DEFAULT_FILL_COLOR;
    }

    /// <summary>
    /// Gets the stroke color from stroke data
    /// </summary>
    /// <param name="strokeData">Stroke data</param>
    /// <returns>Stroke color</returns>
    private static Color GetStrokeColor(JObject strokeData)
    {
        if (strokeData == null)
            return SpriteGenerationConstants.DEFAULT_STROKE_COLOR;

        JObject colorData = strokeData["color"] as JObject;
        if (colorData == null)
            return SpriteGenerationConstants.DEFAULT_STROKE_COLOR;

        float r = colorData["r"]?.Value<float>() ?? 0f;
        float g = colorData["g"]?.Value<float>() ?? 0f;
        float b = colorData["b"]?.Value<float>() ?? 0f;
        float a = strokeData["opacity"]?.Value<float>() ?? 1f;

        return new Color(r, g, b, a);
    }
}

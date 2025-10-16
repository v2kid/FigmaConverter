using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// NodeSpriteGenerator: Converts Figma node styling properties to Unity sprites
/// Process: Figma Node → SVG String → Unity Sprite
///
/// Các bước mà NodeSpriteGenerator làm:
/// 1. Đọc Figma node (SceneNode) - Kiểu: rectangle, frame, path…
/// 2. Thuộc tính: fill, stroke, corner radius, opacity, gradient…
/// 3. Chuyển thành SVG string - Tương tự như HTML + CSS nhưng ở dạng <svg><path ... /></svg>
/// 4. Import SVG sang Unity (SVGParser.ImportSVG) - Unity đọc SVG string → thành SceneInfo gồm các vector geometry
/// 5. Tessellate geometry → Texture (VectorUtils.TessellateScene) - Biến các path vector thành mesh và raster hóa ra Texture2D
/// 6. Tạo Sprite từ Texture2D (Sprite.Create) - Đặt pivot, border, pixels per unit…
/// 
/// Enhanced Features:
/// - Supports strokes with strokeWeight, strokeAlign (INSIDE, OUTSIDE, CENTER)
/// - Supports effects (drop shadow, inner shadow, layer blur, background blur)
/// - Saves generated sprites to Resources/GeneratedSprites for review
/// 
/// Supported Figma Effects:
/// - DROP_SHADOW: Drop shadow with offset, radius, color, and opacity
/// - INNER_SHADOW: Inner shadow with offset, radius, color, and opacity
/// - LAYER_BLUR: Layer blur with radius
/// - BACKGROUND_BLUR: Background blur with radius (approximated in SVG)
/// </summary>
public class NodeSpriteGenerator
{
    private const float SVG_SCALE = 100f; // Scale factor for SVG coordinates
    private const int TEXTURE_SIZE = 512; // Default texture size for sprites
    private const string GENERATED_SPRITES_PATH = "Resources/GeneratedSprites"; // Path to save generated sprites

    /// <summary>
    /// Generates a Unity sprite from Figma node styling properties
    /// </summary>
    /// <param name="nodeData">Figma node data containing styling information</param>
    /// <param name="width">Width of the generated sprite</param>
    /// <param name="height">Height of the generated sprite</param>
    /// <param name="saveToResources">If true, saves the generated sprite to Resources folder</param>
    /// <returns>Generated Unity sprite or null if generation fails</returns>
    public static Sprite GenerateSpriteFromNode(JObject nodeData, float width, float height, bool saveToResources = false)
    {
        try
        {
            // Step 1: Generate SVG string from Figma node properties
            string svgString = GenerateSVGFromNode(nodeData, width, height);

            if (string.IsNullOrEmpty(svgString))
            {
                Debug.LogWarning("Failed to generate SVG string from node");
                return null;
            }

            // Step 2: Convert SVG to Unity sprite
            Sprite sprite = ConvertSVGToSprite(svgString, width, height);

            // Step 3: Save to Resources if requested
            if (sprite != null && saveToResources)
            {
                string nodeName = nodeData["name"]?.ToString() ?? "GeneratedSprite";
                string nodeId = nodeData["id"]?.ToString() ?? Guid.NewGuid().ToString();
                SaveSpriteToResources(sprite, nodeName, nodeId);
            }

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating sprite from node: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates SVG string from Figma node styling properties
    /// </summary>
    public static string GenerateSVGFromNode(JObject nodeData, float width, float height)
    {
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();
        string nodeName = nodeData["name"]?.ToString() ?? "GeneratedNode";

        // Calculate padding for effects (shadows, blur, etc.)
        float effectPadding = CalculateEffectPadding(nodeData);
        float totalWidth = width + (effectPadding * 2);
        float totalHeight = height + (effectPadding * 2);

        // Create SVG with proper dimensions
        StringBuilder svg = new StringBuilder();
        svg.AppendLine(
            $"<svg width=\"{totalWidth * SVG_SCALE}\" height=\"{totalHeight * SVG_SCALE}\" viewBox=\"0 0 {totalWidth * SVG_SCALE} {totalHeight * SVG_SCALE}\" xmlns=\"http://www.w3.org/2000/svg\">"
        );

        // Add definitions for gradients, filters, etc.
        svg.AppendLine(GenerateDefinitions(nodeData));

        // Create a group for the main shape with offset if there are effects
        if (effectPadding > 0)
        {
            svg.AppendLine($"<g transform=\"translate({effectPadding * SVG_SCALE}, {effectPadding * SVG_SCALE})\">");
        }

        // Generate the main shape based on node type
        string shapeElement = GenerateShapeElement(nodeData, width, height);
        if (!string.IsNullOrEmpty(shapeElement))
        {
            svg.AppendLine(shapeElement);
        }

        if (effectPadding > 0)
        {
            svg.AppendLine("</g>");
        }

        svg.AppendLine("</svg>");

        return svg.ToString();
    }

    /// <summary>
    /// Calculates padding needed for effects (shadows, blur)
    /// </summary>
    private static float CalculateEffectPadding(JObject nodeData)
    {
        float maxPadding = 0f;

        JArray effects = nodeData["effects"] as JArray;
        if (effects != null)
        {
            foreach (JObject effect in effects)
            {
                bool visible = effect["visible"]?.ToObject<bool>() ?? true;
                if (!visible)
                    continue;

                string effectType = effect["type"]?.ToString();
                if (effectType == "DROP_SHADOW" || effectType == "INNER_SHADOW")
                {
                    float offsetX = effect["offset"]?["x"]?.ToObject<float>() ?? 0f;
                    float offsetY = effect["offset"]?["y"]?.ToObject<float>() ?? 0f;
                    float radius = effect["radius"]?.ToObject<float>() ?? 0f;

                    float effectExtent = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) + radius;
                    maxPadding = Mathf.Max(maxPadding, effectExtent);
                }
                else if (effectType == "LAYER_BLUR" || effectType == "BACKGROUND_BLUR")
                {
                    float radius = effect["radius"]?.ToObject<float>() ?? 0f;
                    maxPadding = Mathf.Max(maxPadding, radius);
                }
            }
        }

        // Add padding for stroke if stroke is outside
        float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;
        string strokeAlign = nodeData["strokeAlign"]?.ToString() ?? "INSIDE";
        
        if (strokeAlign == "OUTSIDE")
        {
            maxPadding = Mathf.Max(maxPadding, strokeWeight);
        }
        else if (strokeAlign == "CENTER")
        {
            maxPadding = Mathf.Max(maxPadding, strokeWeight * 0.5f);
        }

        return maxPadding;
    }

    /// <summary>
    /// Generates all definitions (gradients, filters, effects) for SVG
    /// </summary>
    private static string GenerateDefinitions(JObject nodeData)
    {
        StringBuilder defs = new StringBuilder();
        defs.AppendLine("<defs>");

        // Generate gradient definitions
        defs.AppendLine(GenerateGradientDefinitions(nodeData));

        // Generate filter definitions for effects
        defs.AppendLine(GenerateEffectDefinitions(nodeData));

        defs.AppendLine("</defs>");
        return defs.ToString();
    }

    /// <summary>
    /// Generates gradient definitions for SVG
    /// </summary>
    private static string GenerateGradientDefinitions(JObject nodeData)
    {
        StringBuilder gradients = new StringBuilder();

        JArray fills = nodeData["fills"] as JArray;
        if (fills != null)
        {
            for (int i = 0; i < fills.Count; i++)
            {
                JObject fill = fills[i] as JObject;
                if (fill != null)
                {
                    string fillType = fill["type"]?.ToString();
                    if (fillType == "GRADIENT_LINEAR" || fillType == "GRADIENT_RADIAL")
                    {
                        gradients.AppendLine(GenerateGradientDefinition(fill, i));
                    }
                }
            }
        }

        return gradients.ToString();
    }

    /// <summary>
    /// Generates effect/filter definitions for SVG (shadows, blur)
    /// </summary>
    private static string GenerateEffectDefinitions(JObject nodeData)
    {
        StringBuilder filters = new StringBuilder();

        JArray effects = nodeData["effects"] as JArray;
        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                JObject effect = effects[i] as JObject;
                if (effect != null)
                {
                    bool visible = effect["visible"]?.ToObject<bool>() ?? true;
                    if (!visible)
                        continue;

                    string effectType = effect["type"]?.ToString();
                    string filterId = $"effect_{i}";

                    if (effectType == "DROP_SHADOW")
                    {
                        filters.AppendLine(GenerateDropShadowFilter(effect, filterId));
                    }
                    else if (effectType == "INNER_SHADOW")
                    {
                        filters.AppendLine(GenerateInnerShadowFilter(effect, filterId));
                    }
                    else if (effectType == "LAYER_BLUR")
                    {
                        filters.AppendLine(GenerateBlurFilter(effect, filterId));
                    }
                    else if (effectType == "BACKGROUND_BLUR")
                    {
                        filters.AppendLine(GenerateBackgroundBlurFilter(effect, filterId));
                    }
                }
            }
        }

        return filters.ToString();
    }

    /// <summary>
    /// Generates drop shadow filter for SVG
    /// </summary>
    private static string GenerateDropShadowFilter(JObject effect, string filterId)
    {
        StringBuilder filter = new StringBuilder();

        float offsetX = effect["offset"]?["x"]?.ToObject<float>() ?? 0f;
        float offsetY = effect["offset"]?["y"]?.ToObject<float>() ?? 0f;
        float radius = effect["radius"]?.ToObject<float>() ?? 0f;
        JObject color = effect["color"] as JObject;

        if (color != null)
        {
            float r = color["r"]?.ToObject<float>() ?? 0f;
            float g = color["g"]?.ToObject<float>() ?? 0f;
            float b = color["b"]?.ToObject<float>() ?? 0f;
            float a = color["a"]?.ToObject<float>() ?? 1f;

            filter.AppendLine($"<filter id=\"{filterId}\" x=\"-50%\" y=\"-50%\" width=\"200%\" height=\"200%\">");
            filter.AppendLine($"  <feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{radius}\" />");
            filter.AppendLine($"  <feOffset dx=\"{offsetX * SVG_SCALE}\" dy=\"{offsetY * SVG_SCALE}\" result=\"offsetblur\" />");
            filter.AppendLine($"  <feFlood flood-color=\"{ColorToHex(new Color(r, g, b, a))}\" flood-opacity=\"{a}\" />");
            filter.AppendLine("  <feComposite in2=\"offsetblur\" operator=\"in\" />");
            filter.AppendLine("  <feMerge>");
            filter.AppendLine("    <feMergeNode />");
            filter.AppendLine("    <feMergeNode in=\"SourceGraphic\" />");
            filter.AppendLine("  </feMerge>");
            filter.AppendLine("</filter>");
        }

        return filter.ToString();
    }

    /// <summary>
    /// Generates inner shadow filter for SVG
    /// </summary>
    private static string GenerateInnerShadowFilter(JObject effect, string filterId)
    {
        StringBuilder filter = new StringBuilder();

        float offsetX = effect["offset"]?["x"]?.ToObject<float>() ?? 0f;
        float offsetY = effect["offset"]?["y"]?.ToObject<float>() ?? 0f;
        float radius = effect["radius"]?.ToObject<float>() ?? 0f;
        JObject color = effect["color"] as JObject;

        if (color != null)
        {
            float r = color["r"]?.ToObject<float>() ?? 0f;
            float g = color["g"]?.ToObject<float>() ?? 0f;
            float b = color["b"]?.ToObject<float>() ?? 0f;
            float a = color["a"]?.ToObject<float>() ?? 1f;

            filter.AppendLine($"<filter id=\"{filterId}\" x=\"-50%\" y=\"-50%\" width=\"200%\" height=\"200%\">");
            filter.AppendLine("  <feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{radius}\" result=\"blur\" />");
            filter.AppendLine($"  <feOffset dx=\"{offsetX * SVG_SCALE}\" dy=\"{offsetY * SVG_SCALE}\" in=\"blur\" result=\"offsetBlur\" />");
            filter.AppendLine("  <feComposite in=\"offsetBlur\" in2=\"SourceAlpha\" operator=\"out\" result=\"inverse\" />");
            filter.AppendLine($"  <feFlood flood-color=\"{ColorToHex(new Color(r, g, b, a))}\" flood-opacity=\"{a}\" result=\"color\" />");
            filter.AppendLine("  <feComposite in=\"color\" in2=\"inverse\" operator=\"in\" result=\"shadow\" />");
            filter.AppendLine("  <feComposite in=\"shadow\" in2=\"SourceGraphic\" operator=\"in\" />");
            filter.AppendLine("</filter>");
        }

        return filter.ToString();
    }

    /// <summary>
    /// Generates blur filter for SVG (layer blur)
    /// </summary>
    private static string GenerateBlurFilter(JObject effect, string filterId)
    {
        float radius = effect["radius"]?.ToObject<float>() ?? 0f;

        StringBuilder filter = new StringBuilder();
        filter.AppendLine($"<filter id=\"{filterId}\">");
        filter.AppendLine($"  <feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"{radius}\" />");
        filter.AppendLine("</filter>");

        return filter.ToString();
    }

    /// <summary>
    /// Generates background blur filter for SVG
    /// Note: Background blur in Figma blurs content behind the element.
    /// In SVG/Unity sprites, we approximate this with a regular blur since
    /// we don't have access to background content.
    /// </summary>
    private static string GenerateBackgroundBlurFilter(JObject effect, string filterId)
    {
        float radius = effect["radius"]?.ToObject<float>() ?? 0f;

        StringBuilder filter = new StringBuilder();
        filter.AppendLine($"<filter id=\"{filterId}\">");
        filter.AppendLine($"  <!-- Background blur approximation -->");
        filter.AppendLine($"  <feGaussianBlur in=\"BackgroundImage\" stdDeviation=\"{radius}\" />");
        filter.AppendLine("</filter>");

        return filter.ToString();
    }

    /// <summary>
    /// Generates a single gradient definition
    /// </summary>
    private static string GenerateGradientDefinition(JObject fill, int index)
    {
        string fillType = fill["type"]?.ToString();
        string gradientId = $"gradient_{index}";

        if (fillType == "GRADIENT_LINEAR")
        {
            return GenerateLinearGradient(fill, gradientId);
        }
        else if (fillType == "GRADIENT_RADIAL")
        {
            return GenerateRadialGradient(fill, gradientId);
        }

        return "";
    }

    /// <summary>
    /// Generates linear gradient definition
    /// </summary>
    private static string GenerateLinearGradient(JObject fill, string gradientId)
    {
        StringBuilder gradient = new StringBuilder();
        gradient.AppendLine(
            $"<linearGradient id=\"{gradientId}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">"
        );

        JArray gradientStops = fill["gradientStops"] as JArray;
        if (gradientStops != null)
        {
            foreach (JObject stop in gradientStops)
            {
                JObject color = stop["color"] as JObject;
                float position = stop["position"]?.ToObject<float>() ?? 0f;
                float opacity = fill["opacity"]?.ToObject<float>() ?? 1f;

                if (color != null)
                {
                    float r = color["r"]?.ToObject<float>() ?? 0f;
                    float g = color["g"]?.ToObject<float>() ?? 0f;
                    float b = color["b"]?.ToObject<float>() ?? 0f;
                    float a = color["a"]?.ToObject<float>() ?? 1f;

                    string hexColor = ColorToHex(new Color(r, g, b, a * opacity));
                    gradient.AppendLine(
                        $"<stop offset=\"{position * 100}%\" stop-color=\"{hexColor}\" />"
                    );
                }
            }
        }

        gradient.AppendLine("</linearGradient>");
        return gradient.ToString();
    }

    /// <summary>
    /// Generates radial gradient definition
    /// </summary>
    private static string GenerateRadialGradient(JObject fill, string gradientId)
    {
        StringBuilder gradient = new StringBuilder();
        gradient.AppendLine(
            $"<radialGradient id=\"{gradientId}\" cx=\"50%\" cy=\"50%\" r=\"50%\">"
        );

        JArray gradientStops = fill["gradientStops"] as JArray;
        if (gradientStops != null)
        {
            foreach (JObject stop in gradientStops)
            {
                JObject color = stop["color"] as JObject;
                float position = stop["position"]?.ToObject<float>() ?? 0f;
                float opacity = fill["opacity"]?.ToObject<float>() ?? 1f;

                if (color != null)
                {
                    float r = color["r"]?.ToObject<float>() ?? 0f;
                    float g = color["g"]?.ToObject<float>() ?? 0f;
                    float b = color["b"]?.ToObject<float>() ?? 0f;
                    float a = color["a"]?.ToObject<float>() ?? 1f;

                    string hexColor = ColorToHex(new Color(r, g, b, a * opacity));
                    gradient.AppendLine(
                        $"<stop offset=\"{position * 100}%\" stop-color=\"{hexColor}\" />"
                    );
                }
            }
        }

        gradient.AppendLine("</radialGradient>");
        return gradient.ToString();
    }

    /// <summary>
    /// Generates the main shape element based on node type
    /// </summary>
    private static string GenerateShapeElement(JObject nodeData, float width, float height)
    {
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        switch (nodeType)
        {
            case "RECTANGLE":
            case "ROUNDED_RECTANGLE":
                return GenerateRectangleElement(nodeData, width, height);
            case "ELLIPSE":
                return GenerateEllipseElement(nodeData, width, height);
            case "VECTOR":
                return GenerateVectorElement(nodeData, width, height);
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
                return GenerateContainerElement(nodeData, width, height);
            default:
                return GenerateRectangleElement(nodeData, width, height); // Default fallback
        }
    }

    /// <summary>
    /// Generates rectangle/rounded rectangle element
    /// </summary>
    private static string GenerateRectangleElement(JObject nodeData, float width, float height)
    {
        // Get corner radius
        float cornerRadius = 0f;
        JToken cornerRadiusToken = nodeData["cornerRadius"];
        if (cornerRadiusToken != null)
        {
            cornerRadius = cornerRadiusToken.ToObject<float>();
        }

        // Check for individual corner radii
        JArray cornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (cornerRadii != null && cornerRadii.Count >= 4)
        {
            // Use individual corner radii
            float topLeft = cornerRadii[0]?.ToObject<float>() ?? 0f;
            float topRight = cornerRadii[1]?.ToObject<float>() ?? 0f;
            float bottomRight = cornerRadii[2]?.ToObject<float>() ?? 0f;
            float bottomLeft = cornerRadii[3]?.ToObject<float>() ?? 0f;

            return GenerateRoundedRectangleWithIndividualCorners(
                nodeData,
                width,
                height,
                topLeft,
                topRight,
                bottomRight,
                bottomLeft
            );
        }

        // Use uniform corner radius
        return GenerateRoundedRectangle(nodeData, width, height, cornerRadius);
    }

    /// <summary>
    /// Generates rounded rectangle with uniform corner radius
    /// </summary>
    private static string GenerateRoundedRectangle(
        JObject nodeData,
        float width,
        float height,
        float cornerRadius
    )
    {
        StringBuilder rect = new StringBuilder();

        // Scale corner radius
        float scaledRadius = cornerRadius * SVG_SCALE;
        float scaledWidth = width * SVG_SCALE;
        float scaledHeight = height * SVG_SCALE;

        // Clamp corner radius to half the smallest dimension
        scaledRadius = Mathf.Min(scaledRadius, Mathf.Min(scaledWidth, scaledHeight) * 0.5f);

        rect.AppendLine(
            $"<rect x=\"0\" y=\"0\" width=\"{scaledWidth}\" height=\"{scaledHeight}\" rx=\"{scaledRadius}\" ry=\"{scaledRadius}\""
        );

        // Apply styling
        string styling = GenerateElementStyling(nodeData);
        rect.AppendLine(styling);

        rect.AppendLine(" />");

        return rect.ToString();
    }

    /// <summary>
    /// Generates rounded rectangle with individual corner radii
    /// </summary>
    private static string GenerateRoundedRectangleWithIndividualCorners(
        JObject nodeData,
        float width,
        float height,
        float topLeft,
        float topRight,
        float bottomRight,
        float bottomLeft
    )
    {
        // For individual corner radii, we need to create a custom path
        StringBuilder path = new StringBuilder();

        float scaledWidth = width * SVG_SCALE;
        float scaledHeight = height * SVG_SCALE;
        float tl = Mathf.Min(topLeft * SVG_SCALE, Mathf.Min(scaledWidth, scaledHeight) * 0.5f);
        float tr = Mathf.Min(topRight * SVG_SCALE, Mathf.Min(scaledWidth, scaledHeight) * 0.5f);
        float br = Mathf.Min(bottomRight * SVG_SCALE, Mathf.Min(scaledWidth, scaledHeight) * 0.5f);
        float bl = Mathf.Min(bottomLeft * SVG_SCALE, Mathf.Min(scaledWidth, scaledHeight) * 0.5f);

        // Create path with rounded corners
        string pathData =
            $"M {tl} 0 "
            + $"L {scaledWidth - tr} 0 "
            + $"Q {scaledWidth} 0 {scaledWidth} {tr} "
            + $"L {scaledWidth} {scaledHeight - br} "
            + $"Q {scaledWidth} {scaledHeight} {scaledWidth - br} {scaledHeight} "
            + $"L {bl} {scaledHeight} "
            + $"Q 0 {scaledHeight} 0 {scaledHeight - bl} "
            + $"L 0 {tl} "
            + $"Q 0 0 {tl} 0 Z";

        path.AppendLine($"<path d=\"{pathData}\"");

        // Apply styling
        string styling = GenerateElementStyling(nodeData);
        path.AppendLine(styling);

        path.AppendLine(" />");

        return path.ToString();
    }

    /// <summary>
    /// Generates ellipse element
    /// </summary>
    private static string GenerateEllipseElement(JObject nodeData, float width, float height)
    {
        StringBuilder ellipse = new StringBuilder();

        float scaledWidth = width * SVG_SCALE;
        float scaledHeight = height * SVG_SCALE;
        float cx = scaledWidth * 0.5f;
        float cy = scaledHeight * 0.5f;
        float rx = scaledWidth * 0.5f;
        float ry = scaledHeight * 0.5f;

        ellipse.AppendLine($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx}\" ry=\"{ry}\"");

        // Apply styling
        string styling = GenerateElementStyling(nodeData);
        ellipse.AppendLine(styling);

        ellipse.AppendLine(" />");

        return ellipse.ToString();
    }

    /// <summary>
    /// Generates vector element (for complex shapes)
    /// </summary>
    private static string GenerateVectorElement(JObject nodeData, float width, float height)
    {
        // For vector nodes, we would need to parse the vector data
        // For now, fallback to rectangle
        return GenerateRectangleElement(nodeData, width, height);
    }

    /// <summary>
    /// Generates container element (frame, group, etc.)
    /// </summary>
    private static string GenerateContainerElement(JObject nodeData, float width, float height)
    {
        // Containers might have background fills
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            return GenerateRectangleElement(nodeData, width, height);
        }

        // If no fills, return empty
        return "";
    }

    /// <summary>
    /// Generates styling attributes for SVG elements
    /// </summary>
    private static string GenerateElementStyling(JObject nodeData)
    {
        StringBuilder styling = new StringBuilder();

        // Apply fills
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            JObject firstFill = fills[0] as JObject;
            if (firstFill != null)
            {
                bool fillVisible = firstFill["visible"]?.ToObject<bool>() ?? true;
                if (fillVisible)
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

                            string hexColor = ColorToHex(new Color(r, g, b, a * opacity));
                            styling.AppendLine($" fill=\"{hexColor}\"");
                            
                            if (opacity < 1f)
                            {
                                styling.AppendLine($" fill-opacity=\"{opacity}\"");
                            }
                        }
                    }
                    else if (fillType == "GRADIENT_LINEAR" || fillType == "GRADIENT_RADIAL")
                    {
                        // Find the gradient index
                        int gradientIndex = 0;
                        for (int i = 0; i < fills.Count; i++)
                        {
                            if (fills[i] == firstFill)
                            {
                                gradientIndex = i;
                                break;
                            }
                        }

                        styling.AppendLine($" fill=\"url(#gradient_{gradientIndex})\"");
                    }
                }
                else
                {
                    styling.AppendLine(" fill=\"none\"");
                }
            }
        }
        else
        {
            // No fill
            styling.AppendLine(" fill=\"none\"");
        }

        // Apply strokes
        JArray strokes = nodeData["strokes"] as JArray;
        float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;
        string strokeAlign = nodeData["strokeAlign"]?.ToString() ?? "INSIDE";

        if (strokes != null && strokes.Count > 0 && strokeWeight > 0)
        {
            JObject firstStroke = strokes[0] as JObject;
            if (firstStroke != null)
            {
                bool strokeVisible = firstStroke["visible"]?.ToObject<bool>() ?? true;
                if (strokeVisible)
                {
                    string strokeType = firstStroke["type"]?.ToString();
                    
                    if (strokeType == "SOLID")
                    {
                        JObject color = firstStroke["color"] as JObject;
                        float opacity = firstStroke["opacity"]?.ToObject<float>() ?? 1f;

                        if (color != null)
                        {
                            float r = color["r"]?.ToObject<float>() ?? 0f;
                            float g = color["g"]?.ToObject<float>() ?? 0f;
                            float b = color["b"]?.ToObject<float>() ?? 0f;
                            float a = color["a"]?.ToObject<float>() ?? 1f;

                            string hexColor = ColorToHex(new Color(r, g, b, a * opacity));
                            styling.AppendLine($" stroke=\"{hexColor}\"");
                            styling.AppendLine($" stroke-width=\"{strokeWeight * SVG_SCALE}\"");

                            if (opacity < 1f)
                            {
                                styling.AppendLine($" stroke-opacity=\"{opacity}\"");
                            }

                            // Stroke cap and join
                            string strokeCap = nodeData["strokeCap"]?.ToString() ?? "NONE";
                            string strokeJoin = nodeData["strokeJoin"]?.ToString() ?? "MITER";

                            switch (strokeCap.ToUpper())
                            {
                                case "ROUND":
                                    styling.AppendLine(" stroke-linecap=\"round\"");
                                    break;
                                case "SQUARE":
                                    styling.AppendLine(" stroke-linecap=\"square\"");
                                    break;
                                default:
                                    styling.AppendLine(" stroke-linecap=\"butt\"");
                                    break;
                            }

                            switch (strokeJoin.ToUpper())
                            {
                                case "ROUND":
                                    styling.AppendLine(" stroke-linejoin=\"round\"");
                                    break;
                                case "BEVEL":
                                    styling.AppendLine(" stroke-linejoin=\"bevel\"");
                                    break;
                                default:
                                    styling.AppendLine(" stroke-linejoin=\"miter\"");
                                    break;
                            }

                            // Handle stroke dashes
                            JArray strokeDashes = nodeData["strokeDashes"] as JArray;
                            if (strokeDashes != null && strokeDashes.Count > 0)
                            {
                                List<string> dashValues = new List<string>();
                                foreach (var dash in strokeDashes)
                                {
                                    dashValues.Add((dash.ToObject<float>() * SVG_SCALE).ToString());
                                }
                                styling.AppendLine($" stroke-dasharray=\"{string.Join(",", dashValues)}\"");
                            }
                        }
                    }
                }
            }
        }

        // Apply effects (filters)
        JArray effects = nodeData["effects"] as JArray;
        if (effects != null && effects.Count > 0)
        {
            List<string> filterIds = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                JObject effect = effects[i] as JObject;
                if (effect != null)
                {
                    bool visible = effect["visible"]?.ToObject<bool>() ?? true;
                    if (visible)
                    {
                        filterIds.Add($"url(#effect_{i})");
                    }
                }
            }

            if (filterIds.Count > 0)
            {
                styling.AppendLine($" filter=\"{string.Join(" ", filterIds)}\"");
            }
        }

        // Apply opacity
        float nodeOpacity = nodeData["opacity"]?.ToObject<float>() ?? 1f;
        if (nodeOpacity < 1f)
        {
            styling.AppendLine($" opacity=\"{nodeOpacity}\"");
        }

        return styling.ToString();
    }

    /// <summary>
    /// Converts Unity Color to hex string
    /// </summary>
    private static string ColorToHex(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Converts SVG string to Unity sprite using SVGSpriteConverter
    /// </summary>
    private static Sprite ConvertSVGToSprite(string svgString, float width, float height)
    {
        // Use the SVGSpriteConverter to convert SVG to sprite
        return SVGSpriteConverter.ConvertSVGToSprite(svgString, width, height);
    }

    /// <summary>
    /// Debug method to save SVG string to file
    /// </summary>
    public static void SaveSVGToFile(string svgString, string fileName)
    {
        try
        {
            string path = Path.Combine(
                Application.dataPath,
                "GeneratedSVGs",
                fileName + ".svg"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, svgString);
            Debug.Log($"SVG saved to: {path}");

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save SVG: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a generated sprite to Resources folder for review and reuse
    /// </summary>
    public static void SaveSpriteToResources(Sprite sprite, string nodeName, string nodeId)
    {
#if UNITY_EDITOR
        try
        {
            // Sanitize file name
            nodeName = nodeName.SanitizeFileName();
            nodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = Path.Combine(Application.dataPath, GENERATED_SPRITES_PATH, nodeId);
            Directory.CreateDirectory(folderPath);

            // Create file path
            string fileName = $"{nodeName}.png";
            string filePath = Path.Combine(folderPath, fileName);

            // Convert sprite to texture and save as PNG
            Texture2D texture = sprite.texture;
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"✓ Saved generated sprite to: Assets/{GENERATED_SPRITES_PATH}/{nodeId}/{fileName}");

            // Refresh AssetDatabase and configure import settings
            AssetDatabase.Refresh();

            string assetPath = $"Assets/{GENERATED_SPRITES_PATH}/{nodeId}/{fileName}";
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save sprite to Resources: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Loads a previously saved sprite from Resources
    /// </summary>
    public static Sprite LoadSpriteFromResources(string nodeName, string nodeId)
    {
        try
        {
            nodeName = nodeName.SanitizeFileName();
            nodeId = nodeId.Replace(":", "-");
            
            string resourcePath = $"GeneratedSprites/{nodeId}/{nodeName}";
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            
            if (sprite != null)
            {
                Debug.Log($"✓ Loaded sprite from Resources: {resourcePath}");
            }
            else
            {
                Debug.LogWarning($"⚠ Sprite not found in Resources: {resourcePath}");
            }
            
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load sprite from Resources: {ex.Message}");
            return null;
        }
    }
}

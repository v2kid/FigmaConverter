using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Handles Figma vector nodes (icons, complex paths, boolean operations)
/// Extracts vector path data and converts to SVG for sprite generation
/// </summary>
public static class FigmaVectorHandler
{
    /// <summary>
    /// Generates SVG from Figma vector node data
    /// Handles vectorPaths, fillGeometry, and strokeGeometry
    /// </summary>
    public static string GenerateSVGFromVector(JObject nodeData, float width, float height)
    {
        StringBuilder svg = new StringBuilder();

        float scale = 100f;
        float padding = FigmaEffectHandler.CalculateEffectPadding(nodeData);
        float totalWidth = width + (padding * 2);
        float totalHeight = height + (padding * 2);

        svg.AppendLine($"<svg width=\"{totalWidth * scale}\" height=\"{totalHeight * scale}\" " +
                      $"viewBox=\"0 0 {totalWidth * scale} {totalHeight * scale}\" " +
                      $"xmlns=\"http://www.w3.org/2000/svg\">");

        if (padding > 0)
        {
            svg.AppendLine($"<g transform=\"translate({padding * scale}, {padding * scale})\">");
        }

        // Try to use fillGeometry first (most accurate)
        string fillGeometry = nodeData["fillGeometry"]?.ToString();
        if (!string.IsNullOrEmpty(fillGeometry))
        {
            svg.AppendLine(GenerateSVGPathFromGeometry(fillGeometry, nodeData, width * scale, height * scale, true));
        }
        else
        {
            // Fallback to vectorPaths
            JArray vectorPaths = nodeData["vectorPaths"] as JArray;
            if (vectorPaths != null && vectorPaths.Count > 0)
            {
                svg.AppendLine(GenerateSVGPathsFromArray(vectorPaths, nodeData, width * scale, height * scale));
            }
            else
            {
                // Last resort: render as basic shape with fills
                svg.AppendLine(FigmaShapeRenderer.GenerateSVGRectangle(nodeData, width * scale, height * scale));
            }
        }

        // Add stroke geometry if present
        string strokeGeometry = nodeData["strokeGeometry"]?.ToString();
        if (!string.IsNullOrEmpty(strokeGeometry) && FigmaStrokeHandler.HasVisibleStroke(nodeData))
        {
            svg.AppendLine(GenerateSVGPathFromGeometry(strokeGeometry, nodeData, width * scale, height * scale, false));
        }

        if (padding > 0)
        {
            svg.AppendLine("</g>");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// Generates SVG path element from Figma geometry data
    /// </summary>
    private static string GenerateSVGPathFromGeometry(
        string geometryData, 
        JObject nodeData, 
        float width, 
        float height,
        bool isFill)
    {
        StringBuilder pathElement = new StringBuilder();

        // Parse geometry data (it's usually SVG path format from Figma)
        JArray geometryArray = null;
        try
        {
            geometryArray = JArray.Parse(geometryData);
        }
        catch
        {
            // If not JSON array, treat as raw SVG path
            return GenerateSinglePath(geometryData, nodeData, isFill);
        }

        if (geometryArray != null && geometryArray.Count > 0)
        {
            foreach (JObject pathObj in geometryArray)
            {
                string pathData = pathObj["path"]?.ToString();
                if (!string.IsNullOrEmpty(pathData))
                {
                    pathElement.AppendLine(GenerateSinglePath(pathData, nodeData, isFill));
                }
            }
        }

        return pathElement.ToString();
    }

    /// <summary>
    /// Generates a single SVG path element
    /// </summary>
    private static string GenerateSinglePath(string pathData, JObject nodeData, bool isFill)
    {
        StringBuilder path = new StringBuilder();
        
        path.Append($"<path d=\"{pathData}\" ");

        if (isFill)
        {
            // Apply fill styling
            string fill = FigmaFillHandler.GenerateSVGFill(nodeData);
            if (fill.Contains("||"))
            {
                string[] parts = fill.Split(new[] { "||" }, System.StringSplitOptions.None);
                path.Append($"{parts[0]} ");
                // Defs will be added separately
            }
            else
            {
                path.Append($"{fill} ");
            }
            path.Append("stroke=\"none\" ");
        }
        else
        {
            // Apply stroke styling
            string stroke = FigmaStrokeHandler.GenerateSVGStroke(nodeData);
            path.Append($"{stroke} fill=\"none\" ");
        }

        // Apply opacity
        float opacity = nodeData["opacity"]?.ToObject<float>() ?? 1f;
        if (opacity < 1f)
        {
            path.Append($"opacity=\"{opacity}\" ");
        }

        path.AppendLine("/>");
        return path.ToString();
    }

    /// <summary>
    /// Generates SVG paths from vectorPaths array
    /// </summary>
    private static string GenerateSVGPathsFromArray(
        JArray vectorPaths, 
        JObject nodeData, 
        float width, 
        float height)
    {
        StringBuilder paths = new StringBuilder();

        foreach (JObject vectorPath in vectorPaths)
        {
            string pathData = vectorPath["data"]?.ToString();
            string windingRule = vectorPath["windingRule"]?.ToString() ?? "NONZERO";

            if (!string.IsNullOrEmpty(pathData))
            {
                paths.Append($"<path d=\"{pathData}\" ");
                
                // Apply fill
                string fill = FigmaFillHandler.GenerateSVGFill(nodeData);
                if (fill.Contains("||"))
                {
                    string[] parts = fill.Split(new[] { "||" }, System.StringSplitOptions.None);
                    paths.Append($"{parts[0]} ");
                }
                else
                {
                    paths.Append($"{fill} ");
                }

                // Apply stroke
                string stroke = FigmaStrokeHandler.GenerateSVGStroke(nodeData);
                paths.Append($"{stroke} ");

                // Apply fill rule
                string fillRule = windingRule == "EVENODD" ? "evenodd" : "nonzero";
                paths.Append($"fill-rule=\"{fillRule}\" ");

                // Apply opacity
                float opacity = nodeData["opacity"]?.ToObject<float>() ?? 1f;
                if (opacity < 1f)
                {
                    paths.Append($"opacity=\"{opacity}\" ");
                }

                paths.AppendLine("/>");
            }
        }

        return paths.ToString();
    }

    /// <summary>
    /// Checks if node has vector data
    /// </summary>
    public static bool HasVectorData(JObject nodeData)
    {
        string fillGeometry = nodeData["fillGeometry"]?.ToString();
        if (!string.IsNullOrEmpty(fillGeometry))
            return true;

        string strokeGeometry = nodeData["strokeGeometry"]?.ToString();
        if (!string.IsNullOrEmpty(strokeGeometry))
            return true;

        JArray vectorPaths = nodeData["vectorPaths"] as JArray;
        if (vectorPaths != null && vectorPaths.Count > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Generates sprite from vector node
    /// </summary>
    public static Sprite GenerateSpriteFromVector(JObject nodeData, float width, float height)
    {
        try
        {
            // Check if we have vector data
            if (!HasVectorData(nodeData))
            {
                Debug.LogWarning("No vector data found, falling back to basic shape rendering");
                return DirectSpriteGenerator.GenerateSpriteFromNodeDirect(nodeData, width, height);
            }

            // Generate SVG from vector data
            string svgString = GenerateSVGFromVector(nodeData, width, height);

            if (string.IsNullOrEmpty(svgString))
            {
                Debug.LogWarning("Failed to generate SVG from vector");
                return null;
            }

            // Convert SVG to sprite
            Sprite sprite = SVGSpriteConverter.ConvertSVGToSprite(svgString, width, height);

            return sprite;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Vector sprite generation failed: {ex.Message}");
            return null;
        }
    }
}


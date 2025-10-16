using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;

/// <summary>
/// SVGSpriteConverter: Converts SVG strings to Unity sprites
/// This class handles the conversion from SVG to Unity's sprite system
/// 
/// Process:
/// 1. Parse SVG string → Vector geometry
/// 2. Tessellate geometry → Texture2D
/// 3. Create Sprite from Texture2D
/// </summary>
public class SVGSpriteConverter
{
    private const int DEFAULT_TEXTURE_SIZE = 512;
    private const float PIXELS_PER_UNIT = 100f;
    
    /// <summary>
    /// Converts SVG string to Unity sprite
    /// </summary>
    /// <param name="svgString">SVG string to convert</param>
    /// <param name="width">Desired width of the sprite</param>
    /// <param name="height">Desired height of the sprite</param>
    /// <param name="textureSize">Size of the generated texture (power of 2 recommended)</param>
    /// <returns>Generated Unity sprite or null if conversion fails</returns>
    public static Sprite ConvertSVGToSprite(string svgString, float width, float height, int textureSize = DEFAULT_TEXTURE_SIZE)
    {
        try
        {
            // Step 1: Parse SVG and extract geometry
            SVGGeometry geometry = ParseSVGGeometry(svgString, width, height);
            
            if (geometry == null)
            {
                Debug.LogWarning("Failed to parse SVG geometry");
                return null;
            }
            
            // Step 2: Tessellate geometry to texture
            Texture2D texture = TessellateGeometryToTexture(geometry, textureSize);
            
            if (texture == null)
            {
                Debug.LogWarning("Failed to tessellate geometry to texture");
                return null;
            }
            
            // Step 3: Create sprite from texture
            Sprite sprite = CreateSpriteFromTexture(texture, width, height);
            
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error converting SVG to sprite: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parses SVG string and extracts geometry information
    /// </summary>
    private static SVGGeometry ParseSVGGeometry(string svgString, float width, float height)
    {
        SVGGeometry geometry = new SVGGeometry();
        geometry.width = width;
        geometry.height = height;
        geometry.shapes = new List<SVGShape>();
        
        // Simple SVG parsing - in a real implementation, you would use a proper SVG parser
        // For now, we'll extract basic shapes and styling information
        
        // Extract viewBox
        string viewBox = ExtractAttribute(svgString, "viewBox");
        if (!string.IsNullOrEmpty(viewBox))
        {
            string[] coords = viewBox.Split(' ');
            if (coords.Length >= 4)
            {
                geometry.viewBoxX = float.Parse(coords[0]);
                geometry.viewBoxY = float.Parse(coords[1]);
                geometry.viewBoxWidth = float.Parse(coords[2]);
                geometry.viewBoxHeight = float.Parse(coords[3]);
            }
        }
        
        // Extract shapes (rect, ellipse, path, etc.)
        ExtractShapes(svgString, geometry);
        
        return geometry;
    }
    
    /// <summary>
    /// Extracts shapes from SVG string
    /// </summary>
    private static void ExtractShapes(string svgString, SVGGeometry geometry)
    {
        // Extract rectangles
        ExtractRectangles(svgString, geometry);
        
        // Extract ellipses
        ExtractEllipses(svgString, geometry);
        
        // Extract paths
        ExtractPaths(svgString, geometry);
    }
    
    /// <summary>
    /// Extracts rectangle elements from SVG
    /// </summary>
    private static void ExtractRectangles(string svgString, SVGGeometry geometry)
    {
        string pattern = @"<rect[^>]*>";
        var matches = System.Text.RegularExpressions.Regex.Matches(svgString, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            SVGShape shape = new SVGShape();
            shape.type = SVGShapeType.Rectangle;
            
            string rectTag = match.Value;
            
            // Extract attributes
            shape.x = ExtractFloatAttribute(rectTag, "x");
            shape.y = ExtractFloatAttribute(rectTag, "y");
            shape.width = ExtractFloatAttribute(rectTag, "width");
            shape.height = ExtractFloatAttribute(rectTag, "height");
            shape.rx = ExtractFloatAttribute(rectTag, "rx");
            shape.ry = ExtractFloatAttribute(rectTag, "ry");
            
            // Extract styling
            ExtractShapeStyling(rectTag, shape);
            
            geometry.shapes.Add(shape);
        }
    }
    
    /// <summary>
    /// Extracts ellipse elements from SVG
    /// </summary>
    private static void ExtractEllipses(string svgString, SVGGeometry geometry)
    {
        string pattern = @"<ellipse[^>]*>";
        var matches = System.Text.RegularExpressions.Regex.Matches(svgString, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            SVGShape shape = new SVGShape();
            shape.type = SVGShapeType.Ellipse;
            
            string ellipseTag = match.Value;
            
            // Extract attributes
            shape.cx = ExtractFloatAttribute(ellipseTag, "cx");
            shape.cy = ExtractFloatAttribute(ellipseTag, "cy");
            shape.rx = ExtractFloatAttribute(ellipseTag, "rx");
            shape.ry = ExtractFloatAttribute(ellipseTag, "ry");
            
            // Extract styling
            ExtractShapeStyling(ellipseTag, shape);
            
            geometry.shapes.Add(shape);
        }
    }
    
    /// <summary>
    /// Extracts path elements from SVG
    /// </summary>
    private static void ExtractPaths(string svgString, SVGGeometry geometry)
    {
        string pattern = @"<path[^>]*>";
        var matches = System.Text.RegularExpressions.Regex.Matches(svgString, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            SVGShape shape = new SVGShape();
            shape.type = SVGShapeType.Path;
            
            string pathTag = match.Value;
            
            // Extract path data
            shape.pathData = ExtractAttribute(pathTag, "d");
            
            // Extract styling
            ExtractShapeStyling(pathTag, shape);
            
            geometry.shapes.Add(shape);
        }
    }
    
    /// <summary>
    /// Extracts styling information from SVG element
    /// </summary>
    private static void ExtractShapeStyling(string elementTag, SVGShape shape)
    {
        // Extract fill
        string fill = ExtractAttribute(elementTag, "fill");
        if (!string.IsNullOrEmpty(fill))
        {
            shape.fillColor = ParseColor(fill);
        }
        
        // Extract stroke
        string stroke = ExtractAttribute(elementTag, "stroke");
        if (!string.IsNullOrEmpty(stroke))
        {
            shape.strokeColor = ParseColor(stroke);
        }
        
        // Extract stroke width
        string strokeWidth = ExtractAttribute(elementTag, "stroke-width");
        if (!string.IsNullOrEmpty(strokeWidth))
        {
            shape.strokeWidth = float.Parse(strokeWidth);
        }
        
        // Extract opacity
        string opacity = ExtractAttribute(elementTag, "opacity");
        if (!string.IsNullOrEmpty(opacity))
        {
            shape.opacity = float.Parse(opacity);
        }
    }
    
    /// <summary>
    /// Extracts attribute value from XML element
    /// </summary>
    private static string ExtractAttribute(string element, string attributeName)
    {
        string pattern = $@"{attributeName}\s*=\s*[""']([^""']*)[""']";
        var match = System.Text.RegularExpressions.Regex.Match(element, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }
    
    /// <summary>
    /// Extracts float attribute value from XML element
    /// </summary>
    private static float ExtractFloatAttribute(string element, string attributeName)
    {
        string value = ExtractAttribute(element, attributeName);
        return float.TryParse(value, out float result) ? result : 0f;
    }
    
    /// <summary>
    /// Parses color string (hex, rgb, etc.) to Unity Color
    /// </summary>
    private static Color ParseColor(string colorString)
    {
        if (string.IsNullOrEmpty(colorString) || colorString == "none")
        {
            return Color.clear;
        }
        
        // Handle hex colors
        if (colorString.StartsWith("#"))
        {
            return HexToColor(colorString);
        }
        
        // Handle rgb colors
        if (colorString.StartsWith("rgb"))
        {
            return RgbToColor(colorString);
        }
        
        // Handle named colors (basic set)
        return NamedColorToColor(colorString);
    }
    
    /// <summary>
    /// Converts hex color string to Unity Color
    /// </summary>
    private static Color HexToColor(string hex)
    {
        hex = hex.Replace("#", "");
        
        if (hex.Length == 3)
        {
            // Short hex format (e.g., #f00)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        }
        
        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
        
        return Color.white;
    }
    
    /// <summary>
    /// Converts RGB color string to Unity Color
    /// </summary>
    private static Color RgbToColor(string rgb)
    {
        string pattern = @"rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)";
        var match = System.Text.RegularExpressions.Regex.Match(rgb, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            int r = int.Parse(match.Groups[1].Value);
            int g = int.Parse(match.Groups[2].Value);
            int b = int.Parse(match.Groups[3].Value);
            
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
        
        return Color.white;
    }
    
    /// <summary>
    /// Converts named color to Unity Color
    /// </summary>
    private static Color NamedColorToColor(string colorName)
    {
        switch (colorName.ToLower())
        {
            case "red": return Color.red;
            case "green": return Color.green;
            case "blue": return Color.blue;
            case "white": return Color.white;
            case "black": return Color.black;
            case "yellow": return Color.yellow;
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            case "gray": return Color.gray;
            case "grey": return Color.gray;
            case "transparent": return Color.clear;
            default: return Color.white;
        }
    }
    
    /// <summary>
    /// Tessellates geometry to texture
    /// </summary>
    private static Texture2D TessellateGeometryToTexture(SVGGeometry geometry, int textureSize)
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[textureSize * textureSize];
        
        // Initialize with transparent background
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Calculate scale factors
        float scaleX = textureSize / geometry.viewBoxWidth;
        float scaleY = textureSize / geometry.viewBoxHeight;
        
        // Render each shape
        foreach (SVGShape shape in geometry.shapes)
        {
            RenderShapeToPixels(shape, pixels, textureSize, scaleX, scaleY);
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    /// <summary>
    /// Renders a single shape to pixel array
    /// </summary>
    private static void RenderShapeToPixels(SVGShape shape, Color[] pixels, int textureSize, float scaleX, float scaleY)
    {
        switch (shape.type)
        {
            case SVGShapeType.Rectangle:
                RenderRectangleToPixels(shape, pixels, textureSize, scaleX, scaleY);
                break;
            case SVGShapeType.Ellipse:
                RenderEllipseToPixels(shape, pixels, textureSize, scaleX, scaleY);
                break;
            case SVGShapeType.Path:
                RenderPathToPixels(shape, pixels, textureSize, scaleX, scaleY);
                break;
        }
    }
    
    /// <summary>
    /// Renders rectangle to pixels
    /// </summary>
    private static void RenderRectangleToPixels(SVGShape shape, Color[] pixels, int textureSize, float scaleX, float scaleY)
    {
        int x = Mathf.RoundToInt(shape.x * scaleX);
        int y = Mathf.RoundToInt(shape.y * scaleY);
        int width = Mathf.RoundToInt(shape.width * scaleX);
        int height = Mathf.RoundToInt(shape.height * scaleY);
        
        // Clamp to texture bounds
        x = Mathf.Clamp(x, 0, textureSize - 1);
        y = Mathf.Clamp(y, 0, textureSize - 1);
        width = Mathf.Clamp(width, 0, textureSize - x);
        height = Mathf.Clamp(height, 0, textureSize - y);
        
        Color fillColor = shape.fillColor;
        fillColor.a *= shape.opacity;
        
        // Fill rectangle
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
            {
                int pixelIndex = py * textureSize + px;
                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    pixels[pixelIndex] = fillColor;
                }
            }
        }
        
        // Draw stroke if present
        if (shape.strokeColor.a > 0 && shape.strokeWidth > 0)
        {
            RenderRectangleStroke(shape, pixels, textureSize, scaleX, scaleY);
        }
    }
    
    /// <summary>
    /// Renders rectangle stroke
    /// </summary>
    private static void RenderRectangleStroke(SVGShape shape, Color[] pixels, int textureSize, float scaleX, float scaleY)
    {
        int x = Mathf.RoundToInt(shape.x * scaleX);
        int y = Mathf.RoundToInt(shape.y * scaleY);
        int width = Mathf.RoundToInt(shape.width * scaleX);
        int height = Mathf.RoundToInt(shape.height * scaleY);
        int strokeWidth = Mathf.RoundToInt(shape.strokeWidth * Mathf.Min(scaleX, scaleY));
        
        Color strokeColor = shape.strokeColor;
        strokeColor.a *= shape.opacity;
        
        // Draw stroke outline
        for (int i = 0; i < strokeWidth; i++)
        {
            // Top and bottom edges
            for (int px = x - i; px < x + width + i; px++)
            {
                SetPixelIfValid(pixels, textureSize, px, y - i, strokeColor);
                SetPixelIfValid(pixels, textureSize, px, y + height + i, strokeColor);
            }
            
            // Left and right edges
            for (int py = y - i; py < y + height + i; py++)
            {
                SetPixelIfValid(pixels, textureSize, x - i, py, strokeColor);
                SetPixelIfValid(pixels, textureSize, x + width + i, py, strokeColor);
            }
        }
    }
    
    /// <summary>
    /// Renders ellipse to pixels
    /// </summary>
    private static void RenderEllipseToPixels(SVGShape shape, Color[] pixels, int textureSize, float scaleX, float scaleY)
    {
        int cx = Mathf.RoundToInt(shape.cx * scaleX);
        int cy = Mathf.RoundToInt(shape.cy * scaleY);
        int rx = Mathf.RoundToInt(shape.rx * scaleX);
        int ry = Mathf.RoundToInt(shape.ry * scaleY);
        
        Color fillColor = shape.fillColor;
        fillColor.a *= shape.opacity;
        
        // Simple ellipse rendering using distance check
        for (int py = cy - ry; py <= cy + ry; py++)
        {
            for (int px = cx - rx; px <= cx + rx; px++)
            {
                if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                {
                    float dx = (px - cx) / (float)rx;
                    float dy = (py - cy) / (float)ry;
                    
                    if (dx * dx + dy * dy <= 1.0f)
                    {
                        int pixelIndex = py * textureSize + px;
                        if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                        {
                            pixels[pixelIndex] = fillColor;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Renders path to pixels (simplified implementation)
    /// </summary>
    private static void RenderPathToPixels(SVGShape shape, Color[] pixels, int textureSize, float scaleX, float scaleY)
    {
        // For now, render as a simple rectangle
        // In a real implementation, you would parse the path data and render the actual path
        Debug.LogWarning("Path rendering not fully implemented - using fallback");
    }
    
    /// <summary>
    /// Sets pixel if coordinates are valid
    /// </summary>
    private static void SetPixelIfValid(Color[] pixels, int textureSize, int x, int y, Color color)
    {
        if (x >= 0 && x < textureSize && y >= 0 && y < textureSize)
        {
            int pixelIndex = y * textureSize + x;
            if (pixelIndex >= 0 && pixelIndex < pixels.Length)
            {
                pixels[pixelIndex] = color;
            }
        }
    }
    
    /// <summary>
    /// Creates Unity sprite from texture
    /// </summary>
    private static Sprite CreateSpriteFromTexture(Texture2D texture, float width, float height)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), // Center pivot
            PIXELS_PER_UNIT
        );
    }
    
    /// <summary>
    /// Debug method to save texture to file
    /// </summary>
    public static void SaveTextureToFile(Texture2D texture, string fileName)
    {
        try
        {
            byte[] pngData = texture.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, "GeneratedTextures", fileName + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pngData);
            Debug.Log($"Texture saved to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save texture: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents SVG geometry data
/// </summary>
public class SVGGeometry
{
    public float width;
    public float height;
    public float viewBoxX;
    public float viewBoxY;
    public float viewBoxWidth;
    public float viewBoxHeight;
    public List<SVGShape> shapes;
}

/// <summary>
/// Represents a single SVG shape
/// </summary>
public class SVGShape
{
    public SVGShapeType type;
    
    // Rectangle properties
    public float x, y, width, height, rx, ry;
    
    // Ellipse properties
    public float cx, cy;
    
    // Path properties
    public string pathData;
    
    // Styling
    public Color fillColor = Color.white;
    public Color strokeColor = Color.clear;
    public float strokeWidth = 0f;
    public float opacity = 1f;
}

/// <summary>
/// SVG shape types
/// </summary>
public enum SVGShapeType
{
    Rectangle,
    Ellipse,
    Path,
    Circle,
    Line,
    Polygon,
    Polyline
}

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NodeSpriteGenerator
{
    private const float SVG_SCALE = 100f;
    private const int TEXTURE_SIZE = 512;

    public static Sprite GenerateSpriteFromNode(JObject nodeData, float width, float height, bool saveToResources = false)
    {
        try
        {
            string svgString = GenerateSVGFromNode(nodeData, width, height);

            if (string.IsNullOrEmpty(svgString))
            {
                Debug.LogWarning("SVG generation failed");
                return null;
            }

            Sprite sprite = ConvertSVGToSprite(svgString, width, height);

            if (sprite != null && saveToResources)
            {
                string nodeName = nodeData["name"]?.ToString() ?? "GeneratedSprite";
                string nodeId = nodeData["id"]?.ToString() ?? System.Guid.NewGuid().ToString();
                SaveSpriteToResources(sprite, nodeName, nodeId);
            }

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Sprite generation error: {ex.Message}");
            return null;
        }
    }

    public static string GenerateSVGFromNode(JObject nodeData, float width, float height)
    {
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        float effectPadding = FigmaEffectHandler.CalculateEffectPadding(nodeData);
        float totalWidth = width + (effectPadding * 2);
        float totalHeight = height + (effectPadding * 2);

        StringBuilder svg = new StringBuilder();
        svg.AppendLine($"<svg width=\"{totalWidth * SVG_SCALE}\" height=\"{totalHeight * SVG_SCALE}\" " +
                      $"viewBox=\"0 0 {totalWidth * SVG_SCALE} {totalHeight * SVG_SCALE}\" " +
                      $"xmlns=\"http://www.w3.org/2000/svg\">");

        if (effectPadding > 0)
        {
            svg.AppendLine($"<g transform=\"translate({effectPadding * SVG_SCALE}, {effectPadding * SVG_SCALE})\">");
        }

        string shapeElement = GenerateShapeElement(nodeData, width * SVG_SCALE, height * SVG_SCALE);
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

    private static string GenerateShapeElement(JObject nodeData, float width, float height)
    {
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        switch (nodeType)
        {
            case "RECTANGLE":
            case "ROUNDED_RECTANGLE":
            case "FRAME":
                return FigmaShapeRenderer.GenerateSVGRectangle(nodeData, width, height);

            case "ELLIPSE":
                return FigmaShapeRenderer.GenerateSVGEllipse(nodeData, width, height);

            default:
                return FigmaShapeRenderer.GenerateSVGRectangle(nodeData, width, height);
        }
    }

    private static Sprite ConvertSVGToSprite(string svgString, float width, float height)
    {
        try
        {
            return SVGSpriteConverter.ConvertSVGToSprite(svgString, width, height);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SVG conversion error: {ex.Message}");
            return null;
        }
    }

    public static void SaveSVGToFile(string svgContent, string nodeName)
    {
#if UNITY_EDITOR
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "Resources", "GeneratedSVG");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string sanitizedName = nodeName.SanitizeFileName();
            string filePath = Path.Combine(folderPath, $"{sanitizedName}.svg");

            File.WriteAllText(filePath, svgContent);
            Debug.Log($"✓ Saved SVG: {sanitizedName}.svg");

            UnityEditor.AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogError($"SVG save failed: {ex.Message}");
        }
#endif
    }

    private static void SaveSpriteToResources(Sprite sprite, string nodeName, string nodeId)
    {
#if UNITY_EDITOR
        try
        {
            string sanitizedName = nodeName.SanitizeFileName();
            string folderPath = Path.Combine(Application.dataPath, "Resources", "GeneratedSprites");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = $"{sanitizedName}.png";
            string filePath = Path.Combine(folderPath, fileName);

            Texture2D texture = sprite.texture;
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            UnityEditor.AssetDatabase.Refresh();

            string assetPath = $"Assets/Resources/GeneratedSprites/{fileName}";
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            if (importer != null)
            {
                importer.textureType = UnityEditor.TextureImporterType.Sprite;
                importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                importer.compressionQuality = 50;
                UnityEditor.EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            Debug.Log($"✓ Saved sprite: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Sprite save failed: {ex.Message}");
        }
#endif
    }
}

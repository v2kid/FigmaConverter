using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Bakes Figma node visual properties (fill, stroke, corner radius, shadow)
/// into a texture using an SDF shader, then saves as a sprite.
/// This avoids rendering children — only the background shape is baked.
/// </summary>
public class ShapeBaker
{
    private readonly FigmaConverterConfig _config;
    private Material _bakerMaterial;

    // Shader property IDs (cached for performance)
    private static readonly int PropFillColor = Shader.PropertyToID("_FillColor");
    private static readonly int PropStrokeColor = Shader.PropertyToID("_StrokeColor");
    private static readonly int PropStrokeWidth = Shader.PropertyToID("_StrokeWidth");
    private static readonly int PropCornerRadius = Shader.PropertyToID("_CornerRadius");
    private static readonly int PropSize = Shader.PropertyToID("_Size");
    private static readonly int PropShadowColor = Shader.PropertyToID("_ShadowColor");
    private static readonly int PropShadowOffset = Shader.PropertyToID("_ShadowOffset");
    private static readonly int PropShadowRadius = Shader.PropertyToID("_ShadowRadius");

    public ShapeBaker(FigmaConverterConfig config)
    {
        _config = config;
        InitMaterial();
    }

    private void InitMaterial()
    {
        Shader shader = Shader.Find("UI/SDFRoundedBox");
        if (shader == null)
        {
            Debug.LogError("ShapeBaker: Cannot find shader 'UI/SDFRoundedBox'");
            return;
        }
        _bakerMaterial = new Material(shader);
    }

    /// <summary>
    /// Checks whether a node needs shape baking (has strokes, effects, or corner radius)
    /// </summary>
    public static bool NeedsShapeBaking(JObject nodeData)
    {
        if (nodeData == null) return false;

        // Check for corner radius
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
        if (cornerRadius > 0) return true;

        // Check for per-corner radius
        JArray cornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (cornerRadii != null && cornerRadii.Count == 4)
        {
            foreach (var r in cornerRadii)
            {
                if (r.ToObject<float>() > 0) return true;
            }
        }

        // Check for strokes
        JArray strokes = nodeData["strokes"] as JArray;
        if (strokes != null && strokes.Count > 0)
        {
            float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;
            if (strokeWeight > 0) return true;
        }

        // Check for shadow effects
        JArray effects = nodeData["effects"] as JArray;
        if (effects != null)
        {
            foreach (JObject effect in effects)
            {
                string type = effect["type"]?.ToString();
                bool visible = effect["visible"]?.ToObject<bool>() ?? true;
                if (visible && (type == "DROP_SHADOW" || type == "INNER_SHADOW"))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Bakes a shape from Figma node data into a sprite and saves it.
    /// Returns the resource path if successful, null otherwise.
    /// </summary>
    public string BakeAndSave(JObject nodeData, string spriteName, float width, float height)
    {
        if (_bakerMaterial == null)
        {
            Debug.LogError("ShapeBaker: Material not initialized");
            return null;
        }

        // Check if already baked
        string sanitizedName = spriteName.SanitizeFileName();
        string nodeIdForPath = _config.targetNodeId.Replace(":", "-");
        string folderPath = Path.Combine(
            Application.dataPath,
            Constant.RESOURCES_FOLDER,
            Constant.SAVE_IMAGE_FOLDER,
            nodeIdForPath
        );

        string filePath = Path.Combine(folderPath, $"{sanitizedName}_bg.png");
        string resourcePath = $"{Constant.SAVE_IMAGE_FOLDER}/{nodeIdForPath}/{sanitizedName}_bg";

        if (File.Exists(filePath))
        {
            return resourcePath;
        }

        // Extract visual properties from Figma data
        SetMaterialProperties(nodeData, width, height);

        // Add padding for shadow
        float shadowPadding = GetShadowPadding(nodeData);
        int texWidth = Mathf.CeilToInt(width + shadowPadding * 2);
        int texHeight = Mathf.CeilToInt(height + shadowPadding * 2);

        // Minimum size
        texWidth = Mathf.Max(texWidth, 4);
        texHeight = Mathf.Max(texHeight, 4);

        // Bake
        Texture2D bakedTexture = RenderToTexture(texWidth, texHeight);
        if (bakedTexture == null) return null;

        // Save
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        byte[] pngData = bakedTexture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);

        UnityEngine.Object.DestroyImmediate(bakedTexture);

#if UNITY_EDITOR
        string assetPath = $"Assets/{Constant.RESOURCES_FOLDER}/{Constant.SAVE_IMAGE_FOLDER}/{nodeIdForPath}/{sanitizedName}_bg.png";
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
#endif

        Debug.Log($"✓ ShapeBaker: Baked shape to {filePath}");
        return resourcePath;
    }

    /// <summary>
    /// Sets shader material properties from Figma node data
    /// </summary>
    private void SetMaterialProperties(JObject nodeData, float width, float height)
    {
        // Size
        _bakerMaterial.SetVector(PropSize, new Vector4(width, height, 0, 0));

        // Fill color
        Color fillColor = ExtractFillColor(nodeData);
        _bakerMaterial.SetColor(PropFillColor, fillColor);

        // Corner radius (per-corner or uniform)
        Vector4 cornerRadius = ExtractCornerRadius(nodeData);
        _bakerMaterial.SetVector(PropCornerRadius, cornerRadius);

        // Stroke
        ExtractStroke(nodeData, out Color strokeColor, out float strokeWidth);
        _bakerMaterial.SetColor(PropStrokeColor, strokeColor);
        _bakerMaterial.SetFloat(PropStrokeWidth, strokeWidth);

        // Shadow (first DROP_SHADOW effect)
        ExtractShadow(nodeData, out Color shadowColor, out Vector2 shadowOffset, out float shadowRadius);
        _bakerMaterial.SetColor(PropShadowColor, shadowColor);
        _bakerMaterial.SetVector(PropShadowOffset, new Vector4(shadowOffset.x, shadowOffset.y, 0, 0));
        _bakerMaterial.SetFloat(PropShadowRadius, shadowRadius);
    }

    private Color ExtractFillColor(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0) return Color.clear;

        foreach (JObject fill in fills)
        {
            string type = fill["type"]?.ToString();
            bool visible = fill["visible"]?.ToObject<bool>() ?? true;
            if (!visible) continue;

            if (type == "SOLID")
            {
                return FigmaColorParser.ParseColorWithOpacity(fill);
            }
        }
        return Color.clear;
    }

    private Vector4 ExtractCornerRadius(JObject nodeData)
    {
        // Per-corner radius
        JArray cornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (cornerRadii != null && cornerRadii.Count == 4)
        {
            return new Vector4(
                cornerRadii[0].ToObject<float>(),  // TL
                cornerRadii[1].ToObject<float>(),  // TR
                cornerRadii[2].ToObject<float>(),  // BR
                cornerRadii[3].ToObject<float>()   // BL
            );
        }

        // Uniform corner radius
        float cr = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;
        return new Vector4(cr, cr, cr, cr);
    }

    private void ExtractStroke(JObject nodeData, out Color strokeColor, out float strokeWidth)
    {
        strokeColor = Color.clear;
        strokeWidth = 0f;

        JArray strokes = nodeData["strokes"] as JArray;
        float weight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;

        if (strokes == null || strokes.Count == 0 || weight <= 0)
            return;

        foreach (JObject stroke in strokes)
        {
            string type = stroke["type"]?.ToString();
            bool visible = stroke["visible"]?.ToObject<bool>() ?? true;
            if (!visible) continue;

            if (type == "SOLID")
            {
                strokeColor = FigmaColorParser.ParseColorWithOpacity(stroke);
                strokeWidth = weight;
                return;
            }
        }
    }

    private void ExtractShadow(JObject nodeData, out Color shadowColor, out Vector2 shadowOffset, out float shadowRadius)
    {
        shadowColor = Color.clear;
        shadowOffset = Vector2.zero;
        shadowRadius = 0f;

        JArray effects = nodeData["effects"] as JArray;
        if (effects == null) return;

        foreach (JObject effect in effects)
        {
            string type = effect["type"]?.ToString();
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible) continue;

            if (type == "DROP_SHADOW")
            {
                JObject color = effect["color"] as JObject;
                if (color != null)
                {
                    float r = color["r"]?.ToObject<float>() ?? 0f;
                    float g = color["g"]?.ToObject<float>() ?? 0f;
                    float b = color["b"]?.ToObject<float>() ?? 0f;
                    float a = color["a"]?.ToObject<float>() ?? 0.25f;
                    shadowColor = new Color(r, g, b, a);
                }

                JObject offset = effect["offset"] as JObject;
                if (offset != null)
                {
                    shadowOffset = new Vector2(
                        offset["x"]?.ToObject<float>() ?? 0f,
                        -(offset["y"]?.ToObject<float>() ?? 0f) // Flip Y for Unity
                    );
                }

                shadowRadius = effect["radius"]?.ToObject<float>() ?? 0f;
                return; // Use first shadow only
            }
        }
    }

    private float GetShadowPadding(JObject nodeData)
    {
        JArray effects = nodeData["effects"] as JArray;
        if (effects == null) return 0f;

        float maxPadding = 0f;
        foreach (JObject effect in effects)
        {
            string type = effect["type"]?.ToString();
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible || type != "DROP_SHADOW") continue;

            float radius = effect["radius"]?.ToObject<float>() ?? 0f;
            JObject offset = effect["offset"] as JObject;
            float offsetX = Mathf.Abs(offset?["x"]?.ToObject<float>() ?? 0f);
            float offsetY = Mathf.Abs(offset?["y"]?.ToObject<float>() ?? 0f);

            float padding = radius + Mathf.Max(offsetX, offsetY);
            maxPadding = Mathf.Max(maxPadding, padding);
        }
        return maxPadding;
    }

    /// <summary>
    /// Renders the shader to a Texture2D
    /// </summary>
    private Texture2D RenderToTexture(int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        GL.Clear(true, true, Color.clear);
        Graphics.Blit(null, rt, _bakerMaterial);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    public void Dispose()
    {
        if (_bakerMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(_bakerMaterial);
            _bakerMaterial = null;
        }
    }
}

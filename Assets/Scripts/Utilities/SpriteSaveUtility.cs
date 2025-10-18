using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utility for saving runtime-generated sprites as persistent assets
/// </summary>
public static class SpriteSaveUtility
{
    /// <summary>
    /// Saves a sprite to the Resources/Sprites folder as a persistent asset
    /// Returns the path to the saved sprite (relative to Resources folder)
    /// </summary>
    public static string SaveSpriteToResources(Sprite sprite, string spriteName, string nodeId)
    {
        if (sprite == null || sprite.texture == null)
        {
            Debug.LogWarning("Cannot save null sprite");
            return null;
        }

#if UNITY_EDITOR
        try
        {
            // Sanitize names
            spriteName = spriteName.SanitizeFileName();
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = Path.Combine(
                Application.dataPath,
                "Resources",
                "Sprites",
                sanitizedNodeId
            );

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Create file path
            string fileName = $"{spriteName}.png";
            string filePath = Path.Combine(folderPath, fileName);

            // Crop texture to sprite bounds to remove padding
            Texture2D croppedTexture = CropTextureToSpriteBounds(sprite);
            if (croppedTexture == null)
            {
                Debug.LogWarning($"Failed to crop sprite {spriteName}");
                return null;
            }

            // Encode cropped texture to PNG
            byte[] pngData = croppedTexture.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogWarning($"Failed to encode sprite {spriteName} to PNG");
                Object.DestroyImmediate(croppedTexture);
                return null;
            }

            // Clean up cropped texture
            Object.DestroyImmediate(croppedTexture);

            // Write to file
            File.WriteAllBytes(filePath, pngData);

            // Get asset path (relative to Assets folder)
            string assetPath = $"Assets/Resources/Sprites/{sanitizedNodeId}/{fileName}";

            // Refresh and configure the texture import settings
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            // Configure as sprite
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            // Return path relative to Resources folder for Resources.Load
            return $"Sprites/{sanitizedNodeId}/{spriteName}";
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save sprite {spriteName}: {ex.Message}");
            return null;
        }
#else
        Debug.LogWarning("Sprite saving only works in Unity Editor");
        return null;
#endif
    }

    /// <summary>
    /// Loads a saved sprite from Resources
    /// </summary>
    public static Sprite LoadSpriteFromResources(string resourcePath)
    {
        return Resources.Load<Sprite>(resourcePath);
    }

    /// <summary>
    /// Checks if a sprite exists in Resources
    /// </summary>
    public static bool SpriteExistsInResources(string spriteName, string nodeId)
    {
        string sanitizedNodeId = nodeId.Replace(":", "-");
        string resourcePath = $"Sprites/{sanitizedNodeId}/{spriteName}";
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        return sprite != null;
    }

    /// <summary>
    /// Crops texture to sprite bounds to remove padding
    /// </summary>
    private static Texture2D CropTextureToSpriteBounds(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return null;

        // Get sprite bounds in texture coordinates
        Rect spriteRect = sprite.textureRect;
        int x = Mathf.RoundToInt(spriteRect.x);
        int y = Mathf.RoundToInt(spriteRect.y);
        int width = Mathf.RoundToInt(spriteRect.width);
        int height = Mathf.RoundToInt(spriteRect.height);

        // Ensure bounds are within texture
        x = Mathf.Clamp(x, 0, sprite.texture.width);
        y = Mathf.Clamp(y, 0, sprite.texture.height);
        width = Mathf.Clamp(width, 1, sprite.texture.width - x);
        height = Mathf.Clamp(height, 1, sprite.texture.height - y);

        // Create new texture with exact sprite dimensions
        Texture2D croppedTexture = new Texture2D(width, height, sprite.texture.format, false);

        // Copy pixels from original texture
        Color[] pixels = sprite.texture.GetPixels(x, y, width, height);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();

        return croppedTexture;
    }
}

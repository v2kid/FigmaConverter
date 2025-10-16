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

            // Encode texture to PNG
            byte[] pngData = sprite.texture.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogWarning($"Failed to encode sprite {spriteName} to PNG");
                return null;
            }

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
}

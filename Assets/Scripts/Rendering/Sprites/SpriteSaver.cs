using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Handles saving sprites and images to the file system
/// Manages file operations, directory creation, and asset database updates
/// </summary>
public static class SpriteSaver
{
    /// <summary>
    /// Saves a sprite to the Resources folder
    /// </summary>
    /// <param name="sprite">Sprite to save</param>
    /// <param name="spriteName">Name for the sprite file</param>
    /// <param name="nodeId">Node ID for organizing files</param>
    /// <returns>True if save was successful</returns>
    public static bool SaveSpriteToResources(Sprite sprite, string spriteName, string nodeId)
    {
        if (sprite == null || sprite.texture == null)
        {
            Debug.LogError("SpriteSaver: Cannot save null sprite or sprite with null texture");
            return false;
        }

        if (string.IsNullOrEmpty(spriteName) || string.IsNullOrEmpty(nodeId))
        {
            Debug.LogError("SpriteSaver: Sprite name and node ID cannot be null or empty");
            return false;
        }

        try
        {
            // Sanitize names for file system
            string sanitizedSpriteName = SanitizeFileName(spriteName);
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = CreateSpriteDirectory(sanitizedNodeId);
            if (string.IsNullOrEmpty(folderPath))
                return false;

            // Create file path
            string fileName = $"{sanitizedSpriteName}{SpriteGenerationConstants.SPRITE_EXTENSION}";
            string filePath = Path.Combine(folderPath, fileName);

            // Crop texture to sprite bounds to remove padding
            Texture2D croppedTexture = CropTextureToSpriteBounds(sprite);
            if (croppedTexture == null)
            {
                Debug.LogWarning($"SpriteSaver: Failed to crop sprite {spriteName}");
                return false;
            }

            // Encode cropped texture to PNG
            byte[] pngData = croppedTexture.EncodeToPNG();
            if (pngData != null && pngData.Length > 0)
            {
                File.WriteAllBytes(filePath, pngData);

#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
                Debug.Log($"✓ SpriteSaver: Saved sprite to Resources: {fileName} at {filePath}");

                // Clean up cropped texture
                UnityEngine.Object.DestroyImmediate(croppedTexture);
                return true;
            }
            else
            {
                Debug.LogWarning($"SpriteSaver: Failed to encode sprite {spriteName} to PNG");
                UnityEngine.Object.DestroyImmediate(croppedTexture);
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error saving sprite {spriteName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Saves an image texture to the Resources folder
    /// </summary>
    /// <param name="texture">Texture to save</param>
    /// <param name="imageName">Name for the image file</param>
    /// <param name="nodeId">Node ID for organizing files</param>
    /// <returns>True if save was successful</returns>
    public static bool SaveImageToResources(Texture2D texture, string imageName, string nodeId)
    {
        if (texture == null)
        {
            Debug.LogError("SpriteSaver: Cannot save null texture");
            return false;
        }

        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
        {
            Debug.LogError("SpriteSaver: Image name and node ID cannot be null or empty");
            return false;
        }

        try
        {
            // Sanitize names for file system
            string sanitizedImageName = SanitizeFileName(imageName);
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = CreateSpriteDirectory(sanitizedNodeId);
            if (string.IsNullOrEmpty(folderPath))
                return false;

            // Create file path
            string fileName = $"{sanitizedImageName}{SpriteGenerationConstants.SPRITE_EXTENSION}";
            string filePath = Path.Combine(folderPath, fileName);

            // Encode texture to PNG
            byte[] pngData = texture.EncodeToPNG();
            if (pngData != null && pngData.Length > 0)
            {
                File.WriteAllBytes(filePath, pngData);

#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
                Debug.Log($"✓ SpriteSaver: Saved image to Resources: {fileName} at {filePath}");
                return true;
            }
            else
            {
                Debug.LogWarning($"SpriteSaver: Failed to encode image {imageName} to PNG");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error saving image {imageName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads a sprite from the Resources folder
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="nodeId">Node ID for file organization</param>
    /// <returns>Loaded sprite or null if not found</returns>
    public static Sprite LoadSpriteFromResources(string imageName, string nodeId)
    {
        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
        {
            Debug.LogWarning("SpriteSaver: Image name and node ID cannot be null or empty");
            return null;
        }

        try
        {
            // Sanitize names
            string sanitizedImageName = SanitizeFileName(imageName);
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create resource path
            string resourcePath =
                $"{SpriteGenerationConstants.SAVE_IMAGE_FOLDER}/{sanitizedNodeId}/{sanitizedImageName}";

            // Load sprite from Resources
            Sprite sprite = Resources.Load<Sprite>(resourcePath);

            if (sprite != null)
            {
                Debug.Log($"SpriteSaver: Loaded sprite from Resources: {resourcePath}");
            }
            else
            {
                Debug.LogWarning($"SpriteSaver: Sprite not found in Resources: {resourcePath}");
            }

            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error loading sprite {imageName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a sprite from Resources with main node ID fallback
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="instanceNodeId">Instance node ID</param>
    /// <param name="mainNodeId">Main node ID for fallback</param>
    /// <returns>Loaded sprite or null if not found</returns>
    public static Sprite LoadSpriteFromResourcesWithMainNodeId(
        string imageName,
        string instanceNodeId,
        string mainNodeId
    )
    {
        if (string.IsNullOrEmpty(imageName))
        {
            Debug.LogWarning("SpriteSaver: Image name cannot be null or empty");
            return null;
        }

        // First try with instance node ID
        if (!string.IsNullOrEmpty(instanceNodeId))
        {
            Sprite sprite = LoadSpriteFromResources(imageName, instanceNodeId);
            if (sprite != null)
                return sprite;
        }

        // Fallback to main node ID
        if (!string.IsNullOrEmpty(mainNodeId) && mainNodeId != instanceNodeId)
        {
            Sprite sprite = LoadSpriteFromResources(imageName, mainNodeId);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    /// <summary>
    /// Crops a texture to the sprite bounds, removing padding
    /// </summary>
    /// <param name="sprite">Sprite to crop</param>
    /// <returns>Cropped texture or null if failed</returns>
    public static Texture2D CropTextureToSpriteBounds(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            Debug.LogError("SpriteSaver: Cannot crop null sprite or sprite with null texture");
            return null;
        }

        try
        {
            // Get sprite bounds in texture coordinates
            Rect spriteRect = sprite.textureRect;

            int x = Mathf.RoundToInt(spriteRect.x);
            int y = Mathf.RoundToInt(spriteRect.y);
            int width = Mathf.RoundToInt(spriteRect.width);
            int height = Mathf.RoundToInt(spriteRect.height);

            // Clamp bounds to texture size
            x = Mathf.Clamp(x, 0, sprite.texture.width);
            y = Mathf.Clamp(y, 0, sprite.texture.height);
            width = Mathf.Clamp(width, 1, sprite.texture.width - x);
            height = Mathf.Clamp(height, 1, sprite.texture.height - y);

            // Create new texture with cropped size
            Texture2D croppedTexture = new Texture2D(width, height, sprite.texture.format, false);

            // Copy pixels from source to cropped texture
            Color[] pixels = sprite.texture.GetPixels(x, y, width, height);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            Debug.Log(
                $"SpriteSaver: Cropped texture from {sprite.texture.width}x{sprite.texture.height} to {width}x{height}"
            );

            return croppedTexture;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error cropping texture: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a directory for sprite storage
    /// </summary>
    /// <param name="nodeId">Node ID for directory name</param>
    /// <returns>Created directory path or null if failed</returns>
    private static string CreateSpriteDirectory(string nodeId)
    {
        try
        {
            // Create directory path
            string folderPath = Path.Combine(
                Application.dataPath,
                SpriteGenerationConstants.RESOURCES_FOLDER,
                SpriteGenerationConstants.SAVE_IMAGE_FOLDER,
                nodeId
            );

            // Create directory if it doesn't exist
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log($"SpriteSaver: Created directory: {folderPath}");
            }

            return folderPath;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"SpriteSaver: Error creating directory for node {nodeId}: {ex.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <returns>Sanitized filename</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "unnamed";

        // Remove invalid characters for file names
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = fileName;

        foreach (char invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Remove additional problematic characters
        sanitized = sanitized
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("\"", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_");

        // Ensure filename is not empty after sanitization
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "unnamed";

        // Limit filename length
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }

        return sanitized;
    }

    /// <summary>
    /// Checks if a sprite exists in Resources
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="nodeId">Node ID for file organization</param>
    /// <returns>True if sprite exists</returns>
    public static bool SpriteExistsInResources(string imageName, string nodeId)
    {
        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
            return false;

        try
        {
            // Sanitize names
            string sanitizedImageName = SanitizeFileName(imageName);
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create resource path
            string resourcePath =
                $"{SpriteGenerationConstants.SAVE_IMAGE_FOLDER}/{sanitizedNodeId}/{sanitizedImageName}";

            // Check if sprite exists
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            return sprite != null;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"SpriteSaver: Error checking if sprite exists {imageName}: {ex.Message}"
            );
            return false;
        }
    }

    /// <summary>
    /// Deletes a sprite from Resources
    /// </summary>
    /// <param name="imageName">Name of the image file</param>
    /// <param name="nodeId">Node ID for file organization</param>
    /// <returns>True if deletion was successful</returns>
    public static bool DeleteSpriteFromResources(string imageName, string nodeId)
    {
        if (string.IsNullOrEmpty(imageName) || string.IsNullOrEmpty(nodeId))
        {
            Debug.LogWarning("SpriteSaver: Image name and node ID cannot be null or empty");
            return false;
        }

        try
        {
            // Sanitize names
            string sanitizedImageName = SanitizeFileName(imageName);
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create file path
            string folderPath = Path.Combine(
                Application.dataPath,
                SpriteGenerationConstants.RESOURCES_FOLDER,
                SpriteGenerationConstants.SAVE_IMAGE_FOLDER,
                sanitizedNodeId
            );

            string fileName = $"{sanitizedImageName}{SpriteGenerationConstants.SPRITE_EXTENSION}";
            string filePath = Path.Combine(folderPath, fileName);

            // Delete file if it exists
            if (File.Exists(filePath))
            {
                File.Delete(filePath);

#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
                Debug.Log($"SpriteSaver: Deleted sprite: {filePath}");
                return true;
            }
            else
            {
                Debug.LogWarning($"SpriteSaver: Sprite file not found: {filePath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error deleting sprite {imageName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clears all sprites for a specific node ID
    /// </summary>
    /// <param name="nodeId">Node ID to clear</param>
    /// <returns>Number of files deleted</returns>
    public static int ClearSpritesForNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            Debug.LogWarning("SpriteSaver: Node ID cannot be null or empty");
            return 0;
        }

        try
        {
            // Sanitize node ID
            string sanitizedNodeId = nodeId.Replace(":", "-");

            // Create directory path
            string folderPath = Path.Combine(
                Application.dataPath,
                SpriteGenerationConstants.RESOURCES_FOLDER,
                SpriteGenerationConstants.SAVE_IMAGE_FOLDER,
                sanitizedNodeId
            );

            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"SpriteSaver: Directory not found: {folderPath}");
                return 0;
            }

            // Get all PNG files in the directory
            string[] files = Directory.GetFiles(
                folderPath,
                $"*{SpriteGenerationConstants.SPRITE_EXTENSION}"
            );

            int deletedCount = 0;
            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"SpriteSaver: Error deleting file {file}: {ex.Message}");
                }
            }

            // Remove directory if empty
            if (Directory.GetFiles(folderPath).Length == 0)
            {
                Directory.Delete(folderPath);
                Debug.Log($"SpriteSaver: Removed empty directory: {folderPath}");
            }

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            Debug.Log($"SpriteSaver: Cleared {deletedCount} sprites for node {nodeId}");
            return deletedCount;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpriteSaver: Error clearing sprites for node {nodeId}: {ex.Message}");
            return 0;
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// Handles image data operations — load from base64, convert bytes to base64.
/// Download-related methods have been removed: downloads are handled directly
/// by FigmaApi using HttpClient for parallel performance.
/// </summary>
public static class ImageRenderer
{
    /// <summary>
    /// Loads a Texture2D from raw base64-encoded image data (PNG or JPG).
    /// Strips the "data:image/...;base64," prefix if present.
    /// </summary>
    public static Texture2D LoadImageFromBase64(string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            Debug.LogError("ImageRenderer: Base64 data is null or empty");
            return null;
        }

        try
        {
            // Strip data-URL prefix if present (e.g. "data:image/png;base64,...")
            if (base64Data.StartsWith("data:"))
            {
                int commaIndex = base64Data.IndexOf(',');
                if (commaIndex >= 0)
                    base64Data = base64Data.Substring(commaIndex + 1);
            }

            byte[] imageData = Convert.FromBase64String(base64Data);
            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogError("ImageRenderer: Decoded image data is empty");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageData))
                return texture;

            Debug.LogError("ImageRenderer: Failed to load image data into texture");
            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error loading image from base64: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts raw image bytes (PNG/JPG) to a base64 string.
    /// </summary>
    public static string ConvertImageDataToBase64(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return string.Empty;

        try
        {
            return Convert.ToBase64String(imageData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImageRenderer: Error converting bytes to base64: {ex.Message}");
            return string.Empty;
        }
    }
}

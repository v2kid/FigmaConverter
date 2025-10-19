using UnityEngine;

/// <summary>
/// Manages texture creation, sizing, and pixel operations for sprite generation
/// Handles optimal texture sizing and pixel array management
/// </summary>
public static class SpriteTextureManager
{
    /// <summary>
    /// Creates a new texture with optimal size based on content dimensions and padding
    /// </summary>
    /// <param name="contentWidth">Width of the content to render</param>
    /// <param name="contentHeight">Height of the content to render</param>
    /// <param name="padding">Padding needed for effects (left, right, top, bottom)</param>
    /// <returns>Newly created Texture2D</returns>
    public static Texture2D CreateOptimalTexture(
        float contentWidth,
        float contentHeight,
        Vector4 padding
    )
    {
        Vector2Int textureSize = CalculateOptimalTextureSize(contentWidth, contentHeight, padding);

        return new Texture2D(
            textureSize.x,
            textureSize.y,
            SpriteGenerationConstants.DEFAULT_TEXTURE_FORMAT,
            SpriteGenerationConstants.DEFAULT_TEXTURE_MIPMAP
        );
    }

    /// <summary>
    /// Calculates the optimal texture size for the given content dimensions and padding
    /// Uses power-of-two sizing for better GPU performance
    /// </summary>
    /// <param name="contentWidth">Width of the content</param>
    /// <param name="contentHeight">Height of the content</param>
    /// <param name="padding">Padding needed (left, right, top, bottom)</param>
    /// <returns>Optimal texture size as Vector2Int</returns>
    public static Vector2Int CalculateOptimalTextureSize(
        float contentWidth,
        float contentHeight,
        Vector4 padding
    )
    {
        // Calculate total dimensions including padding
        float totalWidth = contentWidth + padding.x + padding.y; // left + right
        float totalHeight = contentHeight + padding.z + padding.w; // top + bottom

        // Convert to power-of-two for better GPU performance
        int textureWidth = Mathf.NextPowerOfTwo((int)totalWidth);
        int textureHeight = Mathf.NextPowerOfTwo((int)totalHeight);

        // Apply size constraints
        textureWidth = Mathf.Clamp(
            textureWidth,
            SpriteGenerationConstants.MIN_TEXTURE_SIZE,
            SpriteGenerationConstants.MAX_TEXTURE_SIZE
        );
        textureHeight = Mathf.Clamp(
            textureHeight,
            SpriteGenerationConstants.MIN_TEXTURE_SIZE,
            SpriteGenerationConstants.MAX_TEXTURE_SIZE
        );

        return new Vector2Int(textureWidth, textureHeight);
    }

    /// <summary>
    /// Initializes a pixel array with the specified color
    /// </summary>
    /// <param name="pixelCount">Number of pixels in the array</param>
    /// <param name="color">Color to initialize with</param>
    /// <returns>Initialized color array</returns>
    public static Color[] InitializePixels(int pixelCount, Color color)
    {
        Color[] pixels = new Color[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            pixels[i] = color;
        }
        return pixels;
    }

    /// <summary>
    /// Applies pixel data to a texture
    /// </summary>
    /// <param name="texture">Target texture</param>
    /// <param name="pixels">Pixel data to apply</param>
    public static void ApplyPixelsToTexture(Texture2D texture, Color[] pixels)
    {
        if (texture == null)
        {
            Debug.LogError("SpriteTextureManager: Cannot apply pixels to null texture");
            return;
        }

        if (pixels == null || pixels.Length == 0)
        {
            Debug.LogError("SpriteTextureManager: Cannot apply null or empty pixel array");
            return;
        }

        if (pixels.Length != texture.width * texture.height)
        {
            Debug.LogError(
                $"SpriteTextureManager: Pixel array length ({pixels.Length}) doesn't match texture size ({texture.width * texture.height})"
            );
            return;
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }

    /// <summary>
    /// Calculates the actual content bounds within a texture, excluding padding
    /// </summary>
    /// <param name="pixels">Pixel array to analyze</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="offsetX">X offset of content</param>
    /// <param name="offsetY">Y offset of content</param>
    /// <param name="contentWidth">Expected content width</param>
    /// <param name="contentHeight">Expected content height</param>
    /// <param name="padding">Padding applied (left, right, top, bottom)</param>
    /// <returns>Content bounds as Rect</returns>
    public static Rect CalculateContentBounds(
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        int offsetX,
        int offsetY,
        float contentWidth,
        float contentHeight,
        Vector4 padding
    )
    {
        // Start with the main content area - like DirectSpriteGenerator
        int contentX = offsetX;
        int contentY = offsetY;
        int contentWidthInt = (int)contentWidth;
        int contentHeightInt = (int)contentHeight;

        // Expand bounds to include any visible pixels (like drop shadows)
        // Use directional padding to determine initial bounds - like DirectSpriteGenerator
        int minX = Mathf.Max(0, contentX - (int)padding.x);
        int maxX = Mathf.Min(textureWidth, contentX + contentWidthInt + (int)padding.y);
        int minY = Mathf.Max(0, contentY - (int)padding.z);
        int maxY = Mathf.Min(textureHeight, contentY + contentHeightInt + (int)padding.w);

        // Scan for non-transparent pixels to find actual bounds - like DirectSpriteGenerator
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int index = y * textureWidth + x;
                if (index < pixels.Length && pixels[index].a > 0.01f) // Non-transparent pixel
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x + 1);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }

        // Ensure bounds are within texture - like DirectSpriteGenerator
        minX = Mathf.Clamp(minX, 0, textureWidth);
        minY = Mathf.Clamp(minY, 0, textureHeight);
        maxX = Mathf.Clamp(maxX, minX + 1, textureWidth);
        maxY = Mathf.Clamp(maxY, minY + 1, textureHeight);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Creates a sprite from texture with proper bounds and pivot
    /// </summary>
    /// <param name="texture">Source texture</param>
    /// <param name="bounds">Content bounds within the texture</param>
    /// <param name="pivot">Sprite pivot point</param>
    /// <returns>Created sprite</returns>
    public static Sprite CreateSpriteFromTexture(
        Texture2D texture,
        Rect bounds,
        Vector2 pivot = default
    )
    {
        if (texture == null)
        {
            Debug.LogError("SpriteTextureManager: Cannot create sprite from null texture");
            return null;
        }

        // Validate bounds
        if (bounds.width <= 0 || bounds.height <= 0)
        {
            Debug.LogError(
                $"SpriteTextureManager: Invalid bounds {bounds} - width or height is zero or negative"
            );
            return null;
        }

        if (
            bounds.x < 0
            || bounds.y < 0
            || bounds.x + bounds.width > texture.width
            || bounds.y + bounds.height > texture.height
        )
        {
            Debug.LogError(
                $"SpriteTextureManager: Bounds {bounds} exceed texture size {texture.width}x{texture.height}"
            );
            return null;
        }

        if (pivot == default)
        {
            pivot = SpriteGenerationConstants.DEFAULT_PIVOT;
        }

        try
        {
            return Sprite.Create(texture, bounds, pivot, SpriteGenerationConstants.PIXELS_PER_UNIT);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"SpriteTextureManager: Failed to create sprite with bounds {bounds}: {ex.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Crops a texture to the specified bounds
    /// </summary>
    /// <param name="sourceTexture">Source texture to crop</param>
    /// <param name="bounds">Bounds to crop to</param>
    /// <returns>Cropped texture</returns>
    public static Texture2D CropTexture(Texture2D sourceTexture, Rect bounds)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("SpriteTextureManager: Cannot crop null texture");
            return null;
        }

        int x = Mathf.RoundToInt(bounds.x);
        int y = Mathf.RoundToInt(bounds.y);
        int width = Mathf.RoundToInt(bounds.width);
        int height = Mathf.RoundToInt(bounds.height);

        // Clamp bounds to texture size
        x = Mathf.Clamp(x, 0, sourceTexture.width);
        y = Mathf.Clamp(y, 0, sourceTexture.height);
        width = Mathf.Clamp(width, 1, sourceTexture.width - x);
        height = Mathf.Clamp(height, 1, sourceTexture.height - y);

        // Create new texture with cropped size
        Texture2D croppedTexture = new Texture2D(width, height, sourceTexture.format, false);

        // Copy pixels from source to cropped texture
        Color[] pixels = sourceTexture.GetPixels(x, y, width, height);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();

        return croppedTexture;
    }
}

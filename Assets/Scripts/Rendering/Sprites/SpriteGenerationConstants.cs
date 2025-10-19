using UnityEngine;

/// <summary>
/// Constants used throughout the sprite generation system
/// Centralizes all magic numbers and configuration values
/// </summary>
public static class SpriteGenerationConstants
{
    // Texture Configuration
    public const int DEFAULT_TEXTURE_SIZE = 512;
    public const int MIN_TEXTURE_SIZE = 64;
    public const int MAX_TEXTURE_SIZE = 2048;
    public const float PIXELS_PER_UNIT = 100f;

    // Image Configuration
    public const int DEFAULT_IMAGE_SCALE = 1;
    public const float DEFAULT_OPACITY = 1.0f;
    public const string DEFAULT_IMAGE_FORMAT = "png";
    public const int MAX_IMAGE_RESOLUTION = 4096;

    // Rendering Configuration
    public const float DEFAULT_STROKE_WEIGHT = 1.0f;
    public const float DEFAULT_CORNER_RADIUS = 0.0f;
    public const int DEFAULT_SHADOW_BLUR = 0;

    // File System Configuration
    public const string SPRITE_EXTENSION = ".png";

    // Performance Configuration
    public const int DEFAULT_CACHE_SIZE = 100;
    public const int MAX_CONCURRENT_DOWNLOADS = 5;
    public const float DOWNLOAD_TIMEOUT = 30.0f;

    // Color Configuration
    public static readonly Color DEFAULT_FILL_COLOR = Color.white;
    public static readonly Color DEFAULT_STROKE_COLOR = Color.black;
    public static readonly Color TRANSPARENT_COLOR = Color.clear;

    // Sprite Configuration
    public static readonly Vector2 DEFAULT_PIVOT = new Vector2(0.5f, 0.5f);
    public const TextureFormat DEFAULT_TEXTURE_FORMAT = TextureFormat.RGBA32;
    public const bool DEFAULT_TEXTURE_MIPMAP = false;
}

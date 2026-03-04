using TMPro;
using UnityEngine;

/// <summary>
/// Configuration data for the Figma Converter
/// </summary>
[System.Serializable]
public class FigmaConverterConfig
{
    [Header("API Settings")]
    private string figmaToken = Secrets.FIGMA_TOKEN;
    public string fileId = "YOUR_FILE_ID";
    public string nodeId = "YOUR_NODE_ID";
    private string googleFontsApiKey = Secrets.GOOGLE_FONTS_API_KEY;
    public string fontsPath = "Assets/Fonts";

    [Header("UI Settings")]
    public Canvas targetCanvas;
    public bool createNewCanvas = true;
    public string canvasName = "FigmaUI_Canvas";
    public TMP_FontAsset defaultFont;
    public Color defaultTextColor = Color.black;
    public float scaleFactor = 1f;

    [Header("Image Settings")]
    public string imageFormat = "png";
    public float imageScale = 1f;

    [Header("Performance")]
    public int spriteCacheSize = 100;
    public int nodeCacheSize = 1000;
    public bool enableObjectPooling = true;
    public bool skipInvisibleItems = true;

    // Runtime properties
    [System.NonSerialized]
    public string targetNodeId;

    public FigmaConverterConfig Clone()
    {
        return (FigmaConverterConfig)this.MemberwiseClone();
    }
}

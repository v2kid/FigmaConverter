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
    public TMP_FontAsset defaultFont;
    public float scaleFactor = 1f;

    [Header("Performance")]
    public bool skipInvisibleItems = true;

    // Runtime properties
    [System.NonSerialized]
    public string targetNodeId;
    [System.NonSerialized]
    public Canvas targetCanvas;

    public FigmaConverterConfig Clone()
    {
        return (FigmaConverterConfig)this.MemberwiseClone();
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// Configuration data for the Figma Converter
/// </summary>
[System.Serializable]
public class FigmaConverterConfig
{
    [Header("API Settings")]
    public string figmaToken = "YOUR_FIGMA_TOKEN";
    public string fileId = "YOUR_FILE_ID";
    public string nodeId = "YOUR_NODE_ID";

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

    [Header("Sprite Generation")]
    public bool useSpriteGeneration = true;

    [Header("Performance")]
    public int spriteCacheSize = 100;
    public int nodeCacheSize = 1000;
    public bool enableObjectPooling = true;

    // Runtime properties
    [System.NonSerialized]
    public string targetNodeId;

    public FigmaConverterConfig Clone()
    {
        return (FigmaConverterConfig)this.MemberwiseClone();
    }
}

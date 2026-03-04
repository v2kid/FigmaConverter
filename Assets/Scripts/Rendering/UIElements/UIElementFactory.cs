using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Factory for creating UI elements from Figma nodes
/// Separates creation logic from the main converter
/// </summary>
public class UIElementFactory
{
    private readonly FigmaConverterConfig _config;
    private readonly SpriteCacheService _spriteCache;
    private readonly NodeDataCacheService _nodeCache;
    private readonly GoogleFontService _fontService;
    private readonly ShapeBaker _shapeBaker;

    public UIElementFactory(
        FigmaConverterConfig config,
        SpriteCacheService spriteCache,
        NodeDataCacheService nodeCache,
        GoogleFontService fontService = null,
        ShapeBaker shapeBaker = null
    )
    {
        _config = config;
        _spriteCache = spriteCache;
        _nodeCache = nodeCache;
        _fontService = fontService;
        _shapeBaker = shapeBaker;
    }

    /// <summary>
    /// Creates a UI element based on the node type
    /// </summary>
    public GameObject CreateUIElement(JObject nodeData, Transform parent)
    {
        if (nodeData == null)
            return null;

        string nodeType = nodeData["type"]?.ToString()?.ToUpper();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";

        switch (nodeType)
        {
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
                return CreateContainerElement(nodeData, parent);

            case "TEXT":
                return CreateTextElement(nodeData, parent);

            case "RECTANGLE":
            case "ELLIPSE":
            case "ROUNDED_RECTANGLE":
                return CreateShapeElement(nodeData, parent);

            case "VECTOR":
            case "STAR":
            case "POLYGON":
            case "BOOLEAN_OPERATION":
                return CreateVectorElement(nodeData, parent);

            default:
                return CreateGenericElement(nodeData, parent);
        }
    }

    private GameObject CreateContainerElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Container";
        nodeName = nodeName.SanitizeFileName();

        GameObject container = new GameObject(nodeName);
        container.transform.SetParent(parent, false);
        container.AddComponent<RectTransform>();

        // Check if icon frame
        bool isIconFrame = FigmaIconDetector.IsIconFrame(nodeData);
        if (isIconFrame)
        {
            AddIconImage(container, nodeData);
            return container;
        }

        // Check if has fills or image prefix
        JArray fills = nodeData["fills"] as JArray;
        bool hasFills = fills != null && fills.Count > 0;

        if (hasFills || ShapeBaker.NeedsShapeBaking(nodeData))
        {
            Image backgroundImage = container.AddComponent<Image>();
            ApplyImageScaleMode(nodeData, backgroundImage);

            JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
            float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;

            // Try shape baking first for nodes with strokes/effects/corner radius
            if (_shapeBaker != null && ShapeBaker.NeedsShapeBaking(nodeData))
            {
                string resourcePath = _shapeBaker.BakeAndSave(nodeData, nodeName, width, height);
                if (resourcePath != null)
                {
                    Sprite bakedSprite = Resources.Load<Sprite>(resourcePath);
                    if (bakedSprite != null)
                    {
                        backgroundImage.sprite = bakedSprite;
                        backgroundImage.type = Image.Type.Simple;
                        backgroundImage.color = Color.white;
                    }
                    else
                    {
                        ApplyStyledSprite(container, nodeData, backgroundImage, width, height);
                    }
                }
                else
                {
                    ApplyStyledSprite(container, nodeData, backgroundImage, width, height);
                }
            }
            else
            {
                ApplyStyledSprite(container, nodeData, backgroundImage, width, height);
            }
        }

        return container;
    }

    private GameObject CreateTextElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Text";
        nodeName = nodeName.SanitizeFileName();

        GameObject textGO = new GameObject(nodeName);
        textGO.transform.SetParent(parent, false);
        textGO.AddComponent<RectTransform>();

        TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
        string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
        tmpText.text = characters;
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;

        // Resolve font per node — use fontFamily from Figma data
        string fontFamily = nodeData["style"]?["fontFamily"]?.ToString();
        TMP_FontAsset resolvedFont = _fontService?.GetFontAsset(fontFamily) ?? _config.defaultFont;
        if (resolvedFont != null)
            tmpText.font = resolvedFont;

        ApplyTextStyling(nodeData, tmpText);
        ApplyTextColor(nodeData, tmpText);
        return textGO;
    }

    private GameObject CreateShapeElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Shape";
        nodeName = nodeName.SanitizeFileName();

        GameObject shapeGO = new GameObject(nodeName);
        shapeGO.transform.SetParent(parent, false);
        shapeGO.AddComponent<RectTransform>();

        Image image = shapeGO.AddComponent<Image>();
        ApplyImageScaleMode(nodeData, image);
        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
        float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;

        // Shape baking for nodes with corner radius / strokes / effects
        if (_shapeBaker != null && ShapeBaker.NeedsShapeBaking(nodeData))
        {
            string resourcePath = _shapeBaker.BakeAndSave(nodeData, nodeName, width, height);
            if (resourcePath != null)
            {
                Sprite bakedSprite = Resources.Load<Sprite>(resourcePath);
                if (bakedSprite != null)
                {
                    image.sprite = bakedSprite;
                    image.type = Image.Type.Simple;
                    image.color = Color.white;
                    return shapeGO;
                }
            }
        }

        ApplyStyledSprite(shapeGO, nodeData, image, width, height);
        return shapeGO;
    }

    private GameObject CreateVectorElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Vector";
        nodeName = nodeName.SanitizeFileName();

        GameObject vectorGO = new GameObject(nodeName);
        vectorGO.transform.SetParent(parent, false);
        vectorGO.AddComponent<RectTransform>();

        Image image = vectorGO.AddComponent<Image>();
        ApplyImageScaleMode(nodeData, image);
        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
        float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;
        ApplyVectorSprite(vectorGO, nodeData, image, width, height);

        return vectorGO;
    }

    private GameObject CreateGenericElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "GenericNode";
        nodeName = nodeName.SanitizeFileName();

        GameObject genericGO = new GameObject(nodeName);
        genericGO.transform.SetParent(parent, false);
        genericGO.AddComponent<RectTransform>();

        return genericGO;
    }

    private void AddIconImage(GameObject gameObject, JObject nodeData)
    {
        Image iconImage = gameObject.AddComponent<Image>();
        ApplyImageScaleMode(nodeData, iconImage);

        string nodeName = nodeData["name"]?.ToString() ?? "Icon";
        string sanitizedName = nodeName.SanitizeFileName();

        // Check cache first
        Sprite cachedSprite = _spriteCache.Get(sanitizedName);
        if (cachedSprite != null)
        {
            iconImage.sprite = cachedSprite;
            iconImage.preserveAspect = true;
            return;
        }

        // Load from Resources
        string nodeIdForPath = _config.targetNodeId.Replace(":", "-");
        Sprite iconSprite = Resources.Load<Sprite>($"Sprites/{nodeIdForPath}/{sanitizedName}");

        if (iconSprite != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            _spriteCache.Add(sanitizedName, iconSprite);
        }
        else
        {
            iconImage.color = Color.clear;
        }
    }

    private void ApplyStyledSprite(
        GameObject gameObject,
        JObject nodeData,
        Image image,
        float width,
        float height
    )
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
        string nodeId = nodeData["id"]?.ToString() ?? "";
        string sanitizedName = nodeName.SanitizeFileName();
        string cacheKey = $"{sanitizedName}_{nodeId}";

        // Check cache first
        Sprite cachedSprite = _spriteCache.Get(cacheKey);
        if (cachedSprite != null)
        {
            image.sprite = cachedSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            return;
        }

        // Try to load from saved Resources
        string nodeIdForPath = _config.targetNodeId.Replace(":", "-");
        string resourcePath = $"Sprites/{nodeIdForPath}/{sanitizedName}";
        Sprite savedSprite = Resources.Load<Sprite>(resourcePath);

        if (savedSprite != null)
        {
            image.sprite = savedSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            _spriteCache.Add(cacheKey, savedSprite);
            return;
        }

        // No downloaded sprite available — fallback to simple fill color
        ApplySimpleFill(nodeData, image);
    }


    private void ApplyVectorSprite(
        GameObject gameObject,
        JObject nodeData,
        Image image,
        float width,
        float height
    )
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Unknown";
        string sanitizedName = gameObject.name.SanitizeFileName();

        // Check cache
        Sprite cachedSprite = _spriteCache.Get(sanitizedName);
        if (cachedSprite != null)
        {
            image.sprite = cachedSprite;
            image.preserveAspect = true;
            return;
        }

        // Try loading from Resources
        string nodeIdForPath = _config.targetNodeId.Replace(":", "-");
        Sprite loadedSprite = Resources.Load<Sprite>($"Sprites/{nodeIdForPath}/{sanitizedName}");

        if (loadedSprite != null)
        {
            image.sprite = loadedSprite;
            image.preserveAspect = true;
            _spriteCache.Add(sanitizedName, loadedSprite);
        }
        else
        {
            // Fallback to styled sprite
            ApplyStyledSprite(gameObject, nodeData, image, width, height);
        }
    }

    private void ApplySimpleFill(JObject nodeData, Image image)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            JObject firstFill = fills[0] as JObject;
            if (firstFill?["type"]?.ToString() == "SOLID")
            {
                Color fillColor = FigmaColorParser.ParseColorWithOpacity(firstFill);
                image.color = fillColor;
            }
        }
        else
        {
            image.color = Color.clear;
        }
    }

    /// <summary>
    /// Gets the scaleMode from the first IMAGE fill in node data
    /// </summary>
    private string GetImageScaleMode(JObject nodeData)
    {
        JArray fills = nodeData?["fills"] as JArray;
        if (fills == null) return "FILL";

        foreach (JObject fill in fills)
        {
            if (fill?["type"]?.ToString() == "IMAGE")
            {
                return fill["scaleMode"]?.ToString() ?? "FILL";
            }
        }
        return "FILL";
    }

    /// <summary>
    /// Applies Image type and preserveAspect based on Figma scaleMode.
    /// FIT -> Simple + preserveAspect (image fits within bounds maintaining aspect ratio)
    /// FILL/CROP/default -> Sliced (image stretches to fill)
    /// </summary>
    private void ApplyImageScaleMode(JObject nodeData, Image image)
    {
        string scaleMode = GetImageScaleMode(nodeData);

        if (scaleMode == "FIT")
        {
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        else
        {
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }
    }

    private void ApplyTextStyling(JObject nodeData, TextMeshProUGUI tmpText)
    {

        JObject style = nodeData["style"] as JObject;
        if (style == null)
            return;

        float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
        tmpText.fontSize = fontSize * _config.scaleFactor;

        string textAlignH = style["textAlignHorizontal"]?.ToString();
        string textAlignV = style["textAlignVertical"]?.ToString();

        tmpText.alignment = TextStyleHelper.GetTextAlignment(textAlignH, textAlignV);

        float fontWeight = style["fontWeight"]?.ToObject<float>() ?? 400f;
        tmpText.fontStyle = fontWeight >= 700 ? FontStyles.Bold : FontStyles.Normal;
    }

    private void ApplyTextColor(JObject nodeData, TextMeshProUGUI tmpText)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            JObject firstFill = fills[0] as JObject;
            if (firstFill?["type"]?.ToString() == "SOLID")
            {
                tmpText.color = FigmaColorParser.ParseColorWithOpacity(firstFill);
            }
        }
        else
        {
            tmpText.color = _config.defaultTextColor;
        }
    }
}

/// <summary>
/// Helper for text alignment conversion
/// </summary>
public static class TextStyleHelper
{
    public static TextAlignmentOptions GetTextAlignment(string horizontal, string vertical)
    {
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;

        switch (horizontal?.ToUpper())
        {
            case "CENTER":
                alignment = TextAlignmentOptions.Top;
                break;
            case "RIGHT":
                alignment = TextAlignmentOptions.TopRight;
                break;
            case "JUSTIFIED":
                alignment = TextAlignmentOptions.TopJustified;
                break;
        }

        switch (vertical?.ToUpper())
        {
            case "CENTER":
                alignment = horizontal?.ToUpper() switch
                {
                    "CENTER" => TextAlignmentOptions.Center,
                    "RIGHT" => TextAlignmentOptions.Right,
                    _ => TextAlignmentOptions.Left,
                };
                break;
            case "BOTTOM":
                alignment = horizontal?.ToUpper() switch
                {
                    "CENTER" => TextAlignmentOptions.Bottom,
                    "RIGHT" => TextAlignmentOptions.BottomRight,
                    _ => TextAlignmentOptions.BottomLeft,
                };
                break;
        }

        return alignment;
    }
}

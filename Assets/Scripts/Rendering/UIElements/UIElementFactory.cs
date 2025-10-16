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

    public UIElementFactory(
        FigmaConverterConfig config,
        SpriteCacheService spriteCache,
        NodeDataCacheService nodeCache
    )
    {
        _config = config;
        _spriteCache = spriteCache;
        _nodeCache = nodeCache;
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
        bool hasImagePrefix = nodeName.StartsWith(Constant.IMAGE_PREFIX);

        if (hasFills || hasImagePrefix)
        {
            Image backgroundImage = container.AddComponent<Image>();
            backgroundImage.type = Image.Type.Sliced;

            if (hasImagePrefix)
            {
                ApplyImageSprite(container, nodeName, backgroundImage);
            }
            else if (hasFills)
            {
                JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
                float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
                float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;
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

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Image image = textGO.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            ApplyImageSprite(textGO, nodeName, image);
        }
        else
        {
            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
            string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
            tmpText.text = characters;
            tmpText.textWrappingMode = TextWrappingModes.NoWrap;

            if (_config.defaultFont != null)
                tmpText.font = _config.defaultFont;

            ApplyTextStyling(nodeData, tmpText);
            ApplyTextColor(nodeData, tmpText);
        }

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
        image.type = Image.Type.Sliced;
        
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(shapeGO, nodeName, image);
        }
        else
        {
            JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
            float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;
            ApplyStyledSprite(shapeGO, nodeData, image, width, height);
        }

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
        image.type = Image.Type.Sliced;

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(vectorGO, nodeName, image);
        }
        else
        {
            JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
            float width = boundingBox?["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox?["height"]?.ToObject<float>() ?? 100f;
            ApplyVectorSprite(vectorGO, nodeData, image, width, height);
        }

        return vectorGO;
    }

    private GameObject CreateGenericElement(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "GenericNode";
        nodeName = nodeName.SanitizeFileName();

        GameObject genericGO = new GameObject(nodeName);
        genericGO.transform.SetParent(parent, false);
        genericGO.AddComponent<RectTransform>();

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Image image = genericGO.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            ApplyImageSprite(genericGO, nodeName, image);
        }

        return genericGO;
    }

    private void AddIconImage(GameObject gameObject, JObject nodeData)
    {
        Image iconImage = gameObject.AddComponent<Image>();
        iconImage.type = Image.Type.Sliced;

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

    private void ApplyImageSprite(GameObject gameObject, string nodeName, Image image)
    {
        if (!nodeName.StartsWith(Constant.IMAGE_PREFIX))
            return;

        // Check cache first
        Sprite cachedSprite = _spriteCache.Get(nodeName);
        if (cachedSprite != null)
        {
            image.sprite = cachedSprite;
            image.preserveAspect = true;
            return;
        }

        // Load from Resources
        string nodeIdForPath = _config.targetNodeId.Replace(":", "-");
        Sprite sprite = Resources.Load<Sprite>($"Sprites/{nodeIdForPath}/{gameObject.name}");

        if (sprite != null)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            _spriteCache.Add(nodeName, sprite);
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
        if (!_config.useSpriteGeneration)
        {
            ApplySimpleFill(nodeData, image);
            return;
        }

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
            // Use saved sprite
            image.sprite = savedSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            _spriteCache.Add(cacheKey, savedSprite);
            return;
        }

        // Generate sprite if not found
        Sprite generatedSprite = DirectSpriteGenerator.GenerateSpriteFromNodeDirect(
            nodeData,
            width,
            height
        );
        if (generatedSprite != null)
        {
#if UNITY_EDITOR
            // Save the generated sprite as a persistent asset
            string savedPath = SpriteSaveUtility.SaveSpriteToResources(
                generatedSprite,
                sanitizedName,
                _config.targetNodeId
            );

            if (!string.IsNullOrEmpty(savedPath))
            {
                // Load the saved sprite and use it instead of the runtime one
                Sprite persistentSprite = Resources.Load<Sprite>(savedPath);
                if (persistentSprite != null)
                {
                    image.sprite = persistentSprite;
                    image.preserveAspect = true;
                    image.color = Color.white;
                    _spriteCache.Add(cacheKey, persistentSprite);
                    return;
                }
            }
#endif
            // Fallback to runtime sprite if save failed or not in editor
            image.sprite = generatedSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            _spriteCache.Add(cacheKey, generatedSprite);
        }
        else
        {
            ApplySimpleFill(nodeData, image);
        }
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

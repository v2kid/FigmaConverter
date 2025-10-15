using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FigmaNodeDataConverter : MonoBehaviour, IFigmaNodeConverter
{
    [Header("Node Data Source")]
    public FigmaNodeDataAsset nodeDataAsset;
    public string targetNodeId = "119:441";

    [Header("UI Settings")]
    public Canvas targetCanvas;
    public bool createNewCanvas = true;
    public string canvasName = "FigmaUI_Canvas";
    public TMP_FontAsset defaultFont;
    public Color defaultTextColor = Color.black;
    public float scaleFactor = 1f;

    [Header("Debug")]
    public bool enableDebugLogs = true;


    private Dictionary<string, GameObject> createdNodes = new Dictionary<string, GameObject>();
    private Dictionary<GameObject, Vector2> figmaPositions = new Dictionary<GameObject, Vector2>();


    public void CreateCanvas()
    {
        GameObject canvasGO = new GameObject(canvasName);
        targetCanvas = canvasGO.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Add Canvas Scaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Add GraphicRaycaster
        canvasGO.AddComponent<GraphicRaycaster>();

        if (enableDebugLogs)
            Debug.Log($"✓ Created Canvas: {canvasName}");
    }

    [ContextMenu("Convert Node to UI")]
    public virtual void ConvertNodeToUI()
    {
        if (nodeDataAsset == null)
        {
            Debug.LogError("NodeDataAsset is not assigned!");
            return;
        }

        if (string.IsNullOrEmpty(targetNodeId))
        {
            Debug.LogError("TargetNodeId is not set!");
            return;
        }

        StartCoroutine(ConvertNodeCoroutine());
    }

    private IEnumerator ConvertNodeCoroutine()
    {
        // Ensure we have a target canvas
        if (targetCanvas == null)
        {
            if (createNewCanvas)
            {
                CreateCanvas();
                if (enableDebugLogs)
                    Debug.Log("Created new canvas for UI conversion");
            }
            else
            {
                Debug.LogError("No target canvas found and createNewCanvas is disabled.");
                yield break;
            }
        }

        // Get the document node from the asset
        JObject documentNode = nodeDataAsset.GetDocumentNode(targetNodeId);

        if (documentNode == null)
        {
            Debug.LogError($"Could not find document node for ID: {targetNodeId}");
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Converting node: {documentNode["name"]} (Type: {documentNode["type"]})");
        }

        // Process the document node
        try
        {
            ProcessFigmaNode(documentNode, targetCanvas.transform);

            if (enableDebugLogs)
                Debug.Log("✓ Figma to UI conversion completed!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during conversion: {ex.Message}");
            if (enableDebugLogs)
                Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private GameObject ProcessFigmaNode(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        // Generate fallback ID if null
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = System.Guid.NewGuid().ToString();
            if (enableDebugLogs)
                Debug.LogWarning($"Node ID is null, generated fallback: {nodeId}");
        }

        if (enableDebugLogs)
            Debug.Log($"Processing node: {nodeName} (ID: {nodeId}, Type: {nodeType})");

        GameObject nodeGameObject = null;

        switch (nodeType?.ToUpper())
        {
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
                nodeGameObject = CreateContainerNode(nodeData, parent);
                break;

            case "TEXT":
                nodeGameObject = CreateTextNode(nodeData, parent);
                break;

            case "RECTANGLE":
            case "ELLIPSE":
            case "ROUNDED_RECTANGLE":
                nodeGameObject = CreateImageNode(nodeData, parent);
                break;

            case "VECTOR":
            case "STAR":
            case "POLYGON":
            case "BOOLEAN_OPERATION":
                nodeGameObject = CreateVectorNode(nodeData, parent);
                break;

            default:
                nodeGameObject = CreateGenericNode(nodeData, parent);
                break;
        }

        if (nodeGameObject != null)
        {
            // Only add to dictionary if nodeId is not null or empty
            if (!string.IsNullOrEmpty(nodeId))
            {
                createdNodes[nodeId] = nodeGameObject;
            }

            // Apply common properties
            ApplyTransform(nodeData, nodeGameObject);
            ApplyVisibility(nodeData, nodeGameObject);

            // Process children if they exist
            if (nodeData["children"] is JArray children)
            {
                foreach (JObject child in children)
                {
                    ProcessFigmaNode(child, nodeGameObject.transform);
                }
            }
        }

        return nodeGameObject;
    }

    private GameObject CreateContainerNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Container";
        nodeName = nodeName.SanitizeFileName();

        GameObject container = new GameObject(nodeName);
        container.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = container.AddComponent<RectTransform>();

        // Add Image component for background if fills exist OR if it has image prefix
        JArray fills = nodeData["fills"] as JArray;
        bool hasFills = fills != null && fills.Count > 0;
        bool hasImagePrefix = nodeName.StartsWith(Constant.IMAGE_PREFIX);

        if (hasFills || hasImagePrefix)
        {
            Image backgroundImage = container.AddComponent<Image>();

            // Check for image prefix first
            if (hasImagePrefix)
            {
                ApplyImageSprite(nodeName, backgroundImage);
            }
            else if (hasFills)
            {
                ApplyFills(fills, backgroundImage);
            }
        }

        return container;
    }

    private GameObject CreateTextNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Text";
        nodeName = nodeName.SanitizeFileName();
        GameObject textGO = new GameObject(nodeName);
        textGO.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = textGO.AddComponent<RectTransform>();

        // Check if this text node should be treated as an image
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            // Create as image instead of text
            Image image = textGO.AddComponent<Image>();
            ApplyImageSprite(nodeName, image);

            if (enableDebugLogs)
                Debug.Log($"Text node '{nodeName}' converted to image due to prefix");
        }
        else
        {
            // Add TextMeshProUGUI component
            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();

            // Set text content
            string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
            tmpText.text = characters;
            tmpText.textWrappingMode = TextWrappingModes.NoWrap;

            // Apply font
            if (defaultFont != null)
                tmpText.font = defaultFont;

            // Apply text styling
            ApplyTextStyling(nodeData, tmpText);

            // Apply fills for text color
            JArray fills = nodeData["fills"] as JArray;
            if (fills != null && fills.Count > 0)
            {
                ApplyTextFills(fills, tmpText);
            }
            else
            {
                tmpText.color = defaultTextColor;
            }
        }

        return textGO;
    }

    private GameObject CreateImageNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Image";
        nodeName = nodeName.SanitizeFileName();

        GameObject imageGO = new GameObject(nodeName);
        imageGO.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = imageGO.AddComponent<RectTransform>();

        // Add Image component
        Image image = imageGO.AddComponent<Image>();

        // Check for image prefix first, then apply fills
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(nodeName, image);
        }
        else
        {
            // Apply fills if no sprite was loaded
            JArray fills = nodeData["fills"] as JArray;
            if (fills != null && fills.Count > 0)
            {
                ApplyFills(fills, image);
            }
        }

        // Apply corner radius for rounded rectangles
        string nodeType = nodeData["type"]?.ToString();
        if (nodeType == "RECTANGLE" || nodeType == "ROUNDED_RECTANGLE")
        {
            ApplyCornerRadius(nodeData, imageGO);
        }

        return imageGO;
    }



    private GameObject CreateVectorNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Vector";
        nodeName = nodeName.SanitizeFileName();

        GameObject vectorGO = new GameObject(nodeName);
        vectorGO.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = vectorGO.AddComponent<RectTransform>();

        // Add Image component (vectors will be treated as images)
        Image image = vectorGO.AddComponent<Image>();

        // Check for image prefix first, then apply fills
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(nodeName, image);
        }
        else
        {
            // Apply fills if no sprite was loaded
            JArray fills = nodeData["fills"] as JArray;
            if (fills != null && fills.Count > 0)
            {
                ApplyFills(fills, image);
            }
        }

        return vectorGO;
    }

    private void ApplyImageSprite(string nodeName, Image image)
    {
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Sprite sprite = Resources.Load<Sprite>($"Sprites/{targetNodeId.Replace(":", "-")}/{nodeName}");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;

                if (enableDebugLogs)
                    Debug.Log($"✓ Loaded sprite for {nodeName}");
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"⚠️ Could not find sprite: Sprites/{nodeName}");
            }
        }
    }

    private GameObject CreateGenericNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "GenericNode";
        nodeName = nodeName.SanitizeFileName();
        GameObject genericGO = new GameObject(nodeName);
        genericGO.transform.SetParent(parent, false);

        // Add RectTransform
        RectTransform rectTransform = genericGO.AddComponent<RectTransform>();

        // Check if this generic node should have an image component due to prefix
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Image image = genericGO.AddComponent<Image>();
            ApplyImageSprite(nodeName, image);

            if (enableDebugLogs)
                Debug.Log($"Generic node '{nodeName}' converted to image due to prefix");
        }

        return genericGO;
    }
    private void ApplyTransform(JObject nodeData, GameObject gameObject)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // Get bounding box
        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        if (boundingBox != null)
        {
            float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
            float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
            float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

            // Store original Figma position before scaling
            figmaPositions[gameObject] = new Vector2(x, y);

            // Apply scale factor
            x *= scaleFactor;
            y *= scaleFactor;
            width *= scaleFactor;
            height *= scaleFactor;

            // Set size
            rectTransform.sizeDelta = new Vector2(width, height);

            // Calculate relative position based on parent
            Vector2 relativePosition = CalculateRelativePosition(rectTransform, x, y, width, height);

            // Set position
            rectTransform.anchoredPosition = relativePosition;

            // Set appropriate anchors and pivot based on parent
            SetAnchorsAndPivot(rectTransform);
        }
    }

    private Vector2 CalculateRelativePosition(RectTransform rectTransform, float x, float y, float width, float height)
    {
        // If this is a root canvas child, use canvas-relative positioning
        if (rectTransform.parent == targetCanvas.transform)
        {
            // Get canvas rect for reference
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();

            // Convert Figma coordinates to Unity canvas coordinates
            // Figma (0,0) is top-left, Unity canvas (0,0) is center
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Convert from top-left origin to center origin
            float unityX = x - (canvasWidth * 0.5f);
            float unityY = (canvasHeight * 0.5f) - y; // Flip Y axis

            return new Vector2(unityX, unityY);
        }
        else
        {
            // For nested elements, calculate relative to parent's local coordinate system
            GameObject parentGameObject = rectTransform.parent.gameObject;

            if (figmaPositions.ContainsKey(parentGameObject))
            {
                Vector2 parentFigmaPos = figmaPositions[parentGameObject];

                // Calculate relative position in Figma coordinates (unscaled)
                float relativeX = (x / scaleFactor) - parentFigmaPos.x;
                float relativeY = (y / scaleFactor) - parentFigmaPos.y;

                // Apply scale factor
                relativeX *= scaleFactor;
                relativeY *= scaleFactor;

                // For child elements using top-left anchoring, Y should be negative (down from top)
                // No additional Y-axis flipping needed since we're using top-left anchoring
                return new Vector2(relativeX, -relativeY);
            }
            else
            {
                // Fallback: use absolute position with flipped Y (should rarely happen)
                if (enableDebugLogs)
                    Debug.LogWarning($"Parent Figma position not found for {rectTransform.name}, using absolute positioning");

                return new Vector2(x, -y);
            }
        }
    }

    private void SetAnchorsAndPivot(RectTransform rectTransform)
    {
        // Check if this is a root canvas child
        if (rectTransform.parent == targetCanvas.transform)
        {
            // For canvas children, use center anchoring
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            // For nested elements, use top-left anchoring (matches Figma's coordinate system)
            // This ensures children are positioned relative to parent's top-left corner
            rectTransform.anchorMin = new Vector2(0f, 1f); // Top-left anchor
            rectTransform.anchorMax = new Vector2(0f, 1f); // Top-left anchor
            rectTransform.pivot = new Vector2(0f, 1f); // Top-left pivot
        }
    }

    private void ApplyVisibility(JObject nodeData, GameObject gameObject)
    {
        bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
        gameObject.SetActive(visible);
    }

    private void ApplyFills(JArray fills, Image image)
    {
        if (fills == null || fills.Count == 0) return;

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null) return;

        string fillType = firstFill["type"]?.ToString();

        if (fillType == "SOLID")
        {
            JObject colorObj = firstFill["color"] as JObject;
            if (colorObj != null)
            {
                float r = colorObj["r"]?.ToObject<float>() ?? 1f;
                float g = colorObj["g"]?.ToObject<float>() ?? 1f;
                float b = colorObj["b"]?.ToObject<float>() ?? 1f;
                float a = firstFill["opacity"]?.ToObject<float>() ?? 1f;

                image.color = new Color(r, g, b, a);
            }
        }
    }

    private void ApplyTextFills(JArray fills, TextMeshProUGUI tmpText)
    {
        if (fills == null || fills.Count == 0) return;

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null) return;

        string fillType = firstFill["type"]?.ToString();

        if (fillType == "SOLID")
        {
            JObject colorObj = firstFill["color"] as JObject;
            if (colorObj != null)
            {
                float r = colorObj["r"]?.ToObject<float>() ?? 0f;
                float g = colorObj["g"]?.ToObject<float>() ?? 0f;
                float b = colorObj["b"]?.ToObject<float>() ?? 0f;
                float a = firstFill["opacity"]?.ToObject<float>() ?? 1f;

                tmpText.color = new Color(r, g, b, a);
            }
        }
    }

    private void ApplyTextStyling(JObject nodeData, TextMeshProUGUI tmpText)
    {
        JObject style = nodeData["style"] as JObject;
        if (style == null) return;

        // Font size
        float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
        tmpText.fontSize = fontSize * scaleFactor;

        // Text alignment
        string textAlignHorizontal = style["textAlignHorizontal"]?.ToString();
        string textAlignVertical = style["textAlignVertical"]?.ToString();

        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;

        switch (textAlignHorizontal?.ToUpper())
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

        switch (textAlignVertical?.ToUpper())
        {
            case "CENTER":
                if (textAlignHorizontal?.ToUpper() == "CENTER")
                    alignment = TextAlignmentOptions.Center;
                else if (textAlignHorizontal?.ToUpper() == "RIGHT")
                    alignment = TextAlignmentOptions.Right;
                else
                    alignment = TextAlignmentOptions.Left;
                break;
            case "BOTTOM":
                if (textAlignHorizontal?.ToUpper() == "CENTER")
                    alignment = TextAlignmentOptions.Bottom;
                else if (textAlignHorizontal?.ToUpper() == "RIGHT")
                    alignment = TextAlignmentOptions.BottomRight;
                else
                    alignment = TextAlignmentOptions.BottomLeft;
                break;
        }

        tmpText.alignment = alignment;

        // Font weight (approximate)
        float fontWeight = style["fontWeight"]?.ToObject<float>() ?? 400f;
        if (fontWeight >= 700)
        {
            tmpText.fontStyle = FontStyles.Bold;
        }
        else if (fontWeight >= 500)
        {
            tmpText.fontStyle = FontStyles.Normal;
        }
    }

    private void ApplyCornerRadius(JObject nodeData, GameObject gameObject)
    {
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;

        if (cornerRadius > 0)
        {
            // For simple rounded corners, we might need a custom shader or use UI shapes
            // For now, we'll just log it
            if (enableDebugLogs)
                Debug.Log($"Corner radius {cornerRadius} applied to {gameObject.name}");
        }
    }

    [ContextMenu("Clear Created UI")]
    public void ClearCreatedUI()
    {
        foreach (var kvp in createdNodes)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
            }
        }
        createdNodes.Clear();
        figmaPositions.Clear();

        if (enableDebugLogs)
            Debug.Log("✓ Cleared all created UI elements");
    }

    [ContextMenu("List Available Nodes")]
    public void ListAvailableNodes()
    {
        if (nodeDataAsset == null)
        {
            Debug.LogError("NodeDataAsset is not assigned!");
            return;
        }

        var nodeIds = nodeDataAsset.GetAllNodeIds();
        Debug.Log($"=== Available Nodes ({nodeIds.Count}) ===");

        foreach (string nodeId in nodeIds)
        {
            var nodeData = nodeDataAsset.GetNodeData(nodeId);
            Debug.Log($"• {nodeId}: {nodeData.nodeName} ({nodeData.nodeType})");
        }

        Debug.Log("=== End Node List ===");
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        Debug.Log("=== FigmaNodeDataConverter Setup Validation ===");

        // Check NodeDataAsset
        if (nodeDataAsset == null)
        {
            Debug.LogError("❌ NodeDataAsset is not assigned");
        }
        else
        {
            Debug.Log("✅ NodeDataAsset is assigned");

            if (nodeDataAsset.HasNodeData(targetNodeId))
            {
                Debug.Log($"✅ Target node '{targetNodeId}' found in asset");
            }
            else
            {
                Debug.LogError($"❌ Target node '{targetNodeId}' not found in asset");
            }
        }

        // Check target node ID
        if (string.IsNullOrEmpty(targetNodeId))
        {
            Debug.LogError("❌ targetNodeId is not set");
        }
        else
        {
            Debug.Log($"✅ Target node ID configured: {targetNodeId}");
        }

        // Check Canvas
        if (targetCanvas == null && !createNewCanvas)
        {
            Debug.LogError("❌ No target canvas and createNewCanvas is disabled");
        }
        else
        {
            Debug.Log("✅ Canvas setup is valid");
        }

        // Check Font
        if (defaultFont == null)
        {
            Debug.LogWarning("⚠️ Default font not set - will use default system font");
        }
        else
        {
            Debug.Log($"✅ Default font configured: {defaultFont.name}");
        }

        Debug.Log("=== Validation Complete ===");
    }

    [ContextMenu("Debug Positioning")]
    public void DebugPositioning()
    {
        Debug.Log("=== Position Debug Information ===");

        foreach (var kvp in createdNodes)
        {
            if (kvp.Value != null)
            {
                RectTransform rectTransform = kvp.Value.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    Vector2 figmaPos = figmaPositions.ContainsKey(kvp.Value) ? figmaPositions[kvp.Value] : Vector2.zero;

                    Debug.Log($"Node: {kvp.Value.name}");
                    Debug.Log($"  - Figma Position: {figmaPos}");
                    Debug.Log($"  - Unity Position: {rectTransform.anchoredPosition}");
                    Debug.Log($"  - Size: {rectTransform.sizeDelta}");
                    Debug.Log($"  - Anchors: Min={rectTransform.anchorMin}, Max={rectTransform.anchorMax}");
                    Debug.Log($"  - Pivot: {rectTransform.pivot}");
                    Debug.Log($"  - Parent: {(rectTransform.parent != null ? rectTransform.parent.name : "None")}");
                    Debug.Log("---");
                }
            }
        }

        Debug.Log("=== End Position Debug ===");
    }
    [ContextMenu("Generate Prefabs")]
    public void GeneratePrefab()
    {
        //loop though all game objects, find prefix with tag_prefab_
        foreach (var kvp in createdNodes)
        {
            if (kvp.Value != null && kvp.Value.name.StartsWith(Constant.PREFAB_PREFIX))
            {
                string prefabName = kvp.Value.name.Replace(Constant.PREFAB_PREFIX, "");
                string localPath = $"Assets/Prefabs/{prefabName}.prefab";
                localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

                // Create the Prefab
                PrefabUtility.SaveAsPrefabAssetAndConnect(kvp.Value, localPath, InteractionMode.UserAction);
                if (enableDebugLogs)
                    Debug.Log($"✓ Created prefab: {localPath}");
            }
        }
    }
}
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

public class FigmaNodeDataConverterUIToolkit : MonoBehaviour, IFigmaUXMLConverter
{
    [Header("Node Data Source")]
    public FigmaNodeDataAsset nodeDataAsset;
    public string targetNodeId = "119:441";

    [Header("UI Settings")]
    public UIDocument targetUIDocument;
    public bool createNewUIDocument = true;
    public string documentName = "FigmaUI_Document";
    public StyleSheet defaultStyleSheet;

    [Header("Typography")]
    public FontDefinition defaultFontDefinition;
    public Color defaultTextColor = Color.black;
    public float scaleFactor = 1f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("UXML Generation")]
    public bool generateUXMLFile = true;
    public string uxmlOutputPath = "Assets/UI Toolkit/";
    public string uxmlFileName = "GeneratedUI";
    public bool generateUSSFile = true;
    public string ussOutputPath = "Assets/UI Toolkit/";
    public string ussFileName = "GeneratedStyles";

    private Dictionary<string, VisualElement> createdNodes = new Dictionary<string, VisualElement>();
    private Dictionary<VisualElement, Vector2> figmaPositions = new Dictionary<VisualElement, Vector2>();
    private VisualElement rootVisualElement;

    // UXML generation data
    private StringBuilder uxmlBuilder = new StringBuilder();
    private StringBuilder ussBuilder = new StringBuilder();
    private int indentLevel = 0;
    private Dictionary<string, Vector2> figmaAbsolutePositions = new Dictionary<string, Vector2>();
    private Stack<string> parentNodeStack = new Stack<string>(); // Track parent hierarchy


    public void CreateUIDocument()
    {
        GameObject documentGO = new GameObject(documentName);
        targetUIDocument = documentGO.AddComponent<UIDocument>();

        // For runtime UI creation, we don't need a VisualTreeAsset
        // The UIDocument will create a default root element automatically

        if (enableDebugLogs)
            Debug.Log($"Created UI Document GameObject: {documentName}");
    }

    private void EnsureUIDocumentReady()
    {
        if (targetUIDocument == null) return;

        // Get the rootVisualElement
        rootVisualElement = targetUIDocument.rootVisualElement;

        if (rootVisualElement == null)
        {
            if (enableDebugLogs)
                Debug.LogError("RootVisualElement is null - UIDocument may not be properly initialized");
            return;
        }

        // Apply default style sheet if available
        if (defaultStyleSheet != null)
        {
            if (!rootVisualElement.styleSheets.Contains(defaultStyleSheet))
            {
                rootVisualElement.styleSheets.Add(defaultStyleSheet);
            }
        }

        // Ensure the root element has proper sizing
        rootVisualElement.style.width = Length.Percent(100);
        rootVisualElement.style.height = Length.Percent(100);
        rootVisualElement.style.flexDirection = FlexDirection.Column;

        if (enableDebugLogs)
            Debug.Log($"✓ UI Document ready. Root size: {rootVisualElement.resolvedStyle.width}x{rootVisualElement.resolvedStyle.height}");
    }

    [ContextMenu("Convert Node to UI")]
    public void ConvertNodeToUI()
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

        StartCoroutine(ConvertNodeCoroutineUIToolkit());
    }

    [ContextMenu("Generate UXML File")]
    public void GenerateUXMLFile()
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

        StartCoroutine(GenerateUXMLCoroutine());
    }

    [ContextMenu("Test Create Simple Element")]
    public void TestCreateSimpleElement()
    {
        // Ensure we have a proper setup
        EnsureUIDocumentSetup();

        if (rootVisualElement == null)
        {
            Debug.LogError("Failed to get rootVisualElement after setup");
            return;
        }

        // Clear existing content
        rootVisualElement.Clear();

        // Create a simple test element
        var testElement = new Label("TEST - If you see this, UI Toolkit is working!");
        testElement.style.backgroundColor = Color.red;
        testElement.style.color = Color.white;
        testElement.style.fontSize = 20;
        testElement.style.width = 400;
        testElement.style.height = 100;
        testElement.style.position = Position.Absolute;
        testElement.style.left = 50;
        testElement.style.top = 50;
        testElement.style.unityTextAlign = TextAnchor.MiddleCenter;

        rootVisualElement.Add(testElement);

        Debug.Log($"✓ Created test element. Root children count: {rootVisualElement.childCount}");
        Debug.Log($"✓ Root element size: {rootVisualElement.resolvedStyle.width}x{rootVisualElement.resolvedStyle.height}");
        Debug.Log($"✓ UIDocument GameObject: {targetUIDocument?.gameObject.name}");
        Debug.Log($"✓ UXML Asset: {(targetUIDocument?.visualTreeAsset != null ? "Assigned" : "None")}");
    }

    private void EnsureUIDocumentSetup()
    {
        // If no UIDocument is assigned, create one
        if (targetUIDocument == null)
        {
            if (createNewUIDocument)
            {
                CreateUIDocument();
                Debug.Log("Created new UIDocument");
            }
            else
            {
                Debug.LogError("No UIDocument assigned and createNewUIDocument is disabled");
                return;
            }
        }

        // Ensure we have the root element
        if (rootVisualElement == null)
        {
            rootVisualElement = targetUIDocument.rootVisualElement;
            Debug.Log($"Got rootVisualElement: {rootVisualElement != null}");
        }

        // Apply basic setup
        if (rootVisualElement != null)
        {
            EnsureUIDocumentReady();
        }
    }

    private IEnumerator ConvertNodeCoroutineUIToolkit()
    {
        // Ensure proper setup
        EnsureUIDocumentSetup();

        // Wait a frame for UIDocument to initialize properly
        yield return null;

        // Final check for rootVisualElement
        if (rootVisualElement == null)
        {
            Debug.LogError("Failed to initialize rootVisualElement. UIDocument may not be properly configured.");
            yield break;
        }

        // Get the document node from the asset
        if (enableDebugLogs)
        {
            Debug.Log($"Attempting to get document node for ID: {targetNodeId}");
        }

        JObject documentNode = nodeDataAsset.GetDocumentNode(targetNodeId);

        if (documentNode == null)
        {
            Debug.LogError($"Could not find document node for ID: {targetNodeId}");

            // Debug: List all available nodes
            if (enableDebugLogs)
            {
                var availableNodes = nodeDataAsset.GetAllNodeIds();
                Debug.Log($"Available node IDs ({availableNodes.Count}):");
                foreach (string id in availableNodes)
                {
                    var nodeData = nodeDataAsset.GetNodeData(id);
                    Debug.Log($"  - {id}: {nodeData?.nodeName} ({nodeData?.nodeType})");
                }
            }
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Converting node: {documentNode["name"]} (Type: {documentNode["type"]})");
            Debug.Log($"Document node JSON: {documentNode.ToString()}");
        }

        // Clear existing content
        rootVisualElement.Clear();

        if (enableDebugLogs)
        {
            Debug.Log($"Root element info - Size: {rootVisualElement.resolvedStyle.width}x{rootVisualElement.resolvedStyle.height}");
            Debug.Log($"Root element children before processing: {rootVisualElement.childCount}");
        }

        // Process the document node
        try
        {
            VisualElement createdElement = ProcessFigmaNode(documentNode, rootVisualElement);

            if (enableDebugLogs)
            {
                Debug.Log($"Root element children after processing: {rootVisualElement.childCount}");
                Debug.Log($"Created element: {createdElement?.name ?? "null"}");
                if (createdElement != null)
                {
                    Debug.Log($"Created element size: {createdElement.resolvedStyle.width}x{createdElement.resolvedStyle.height}");
                    Debug.Log($"Created element position: {createdElement.resolvedStyle.left}, {createdElement.resolvedStyle.top}");
                    Debug.Log($"Created element display: {createdElement.resolvedStyle.display}");
                }
                Debug.Log("✓ Figma to UI Toolkit conversion completed!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during conversion: {ex.Message}");
            if (enableDebugLogs)
                Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private IEnumerator GenerateUXMLCoroutine()
    {
        // Get the document node from the asset
        if (enableDebugLogs)
        {
            Debug.Log($"Generating UXML for node ID: {targetNodeId}");
        }

        JObject documentNode = nodeDataAsset.GetDocumentNode(targetNodeId);

        if (documentNode == null)
        {
            Debug.LogError($"Could not find document node for ID: {targetNodeId}");
            yield break;
        }

        // Initialize UXML and USS builders
        InitializeUXMLGeneration();

        bool success = false;
        try
        {
            // Start UXML document
            StartUXMLDocument();

            // Process the document node for UXML generation
            ProcessFigmaNodeForUXML(documentNode);

            // End UXML document
            EndUXMLDocument();

            success = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during UXML generation: {ex.Message}");
            if (enableDebugLogs)
                Debug.LogError($"Stack trace: {ex.StackTrace}");
        }

        // Save files outside of try-catch to avoid yield return restrictions
        if (success)
        {
            yield return StartCoroutine(SaveGeneratedFiles());

            if (enableDebugLogs)
                Debug.Log("✓ UXML and USS files generated successfully!");
        }
    }

    private VisualElement ProcessFigmaNode(JObject nodeData, VisualElement parent)
    {
        if (nodeData == null)
        {
            Debug.LogError("ProcessFigmaNode called with null nodeData");
            return null;
        }

        if (parent == null)
        {
            Debug.LogError("ProcessFigmaNode called with null parent");
            return null;
        }

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

        VisualElement nodeElement = null;

        switch (nodeType?.ToUpper())
        {
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
                nodeElement = CreateContainerElement(nodeData, parent);
                break;

            case "TEXT":
                nodeElement = CreateTextElement(nodeData, parent);
                break;

            case "RECTANGLE":
            case "ELLIPSE":
            case "ROUNDED_RECTANGLE":
                nodeElement = CreateImageElement(nodeData, parent);
                break;

            case "VECTOR":
            case "STAR":
            case "POLYGON":
            case "BOOLEAN_OPERATION":
                nodeElement = CreateVectorElement(nodeData, parent);
                break;

            default:
                nodeElement = CreateGenericElement(nodeData, parent);
                break;
        }

        if (nodeElement != null)
        {
            // Set element name and add USS class
            nodeElement.name = nodeName;
            nodeElement.AddToClassList("figma-element");
            nodeElement.AddToClassList($"figma-{nodeType?.ToLower()}");

            // Only add to dictionary if nodeId is not null or empty
            if (!string.IsNullOrEmpty(nodeId))
            {
                createdNodes[nodeId] = nodeElement;
            }

            // Apply common properties
            ApplyTransform(nodeData, nodeElement);
            ApplyVisibility(nodeData, nodeElement);

            // Add to parent
            parent.Add(nodeElement);

            if (enableDebugLogs)
            {
                Debug.Log($"Created element '{nodeName}' - Type: {nodeType}, Size: {nodeElement.style.width.value}x{nodeElement.style.height.value}, Position: {nodeElement.style.left.value},{nodeElement.style.top.value}");
            }

            // Process children if they exist
            if (nodeData["children"] is JArray children)
            {
                foreach (JObject child in children)
                {
                    ProcessFigmaNode(child, nodeElement);
                }
            }
        }

        return nodeElement;
    }

    private VisualElement CreateContainerElement(JObject nodeData, VisualElement parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Container";

        VisualElement container = new VisualElement();
        container.AddToClassList("figma-container");

        // Apply background fills if they exist
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            ApplyFills(fills, container);
        }

        // Enable clipping by default for containers
        container.style.overflow = Overflow.Hidden;

        return container;
    }

    private VisualElement CreateTextElement(JObject nodeData, VisualElement parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Text";

        Label textElement = new Label();
        textElement.AddToClassList("figma-text");

        // Set text content
        string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
        textElement.text = characters;

        // Apply font
        if (defaultFontDefinition.fontAsset != null)
        {
            textElement.style.unityFontDefinition = defaultFontDefinition;
        }

        // Apply text styling
        ApplyTextStyling(nodeData, textElement);

        // Apply fills for text color
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            ApplyTextFills(fills, textElement);
        }
        else
        {
            textElement.style.color = defaultTextColor;
        }

        return textElement;
    }

    private VisualElement CreateImageElement(JObject nodeData, VisualElement parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Image";

        VisualElement imageElement = new VisualElement();
        imageElement.AddToClassList("figma-image");

        // Apply fills for background
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            ApplyFills(fills, imageElement);
        }

        // Apply corner radius for rounded rectangles
        string nodeType = nodeData["type"]?.ToString();
        if (nodeType == "RECTANGLE" || nodeType == "ROUNDED_RECTANGLE")
        {
            ApplyCornerRadius(nodeData, imageElement);
        }

        // Handle ellipse shapes
        if (nodeType == "ELLIPSE")
        {
            // Create circular shape by setting border radius to 50%
            var borderRadius = new StyleLength(new Length(50, LengthUnit.Percent));
            imageElement.style.borderTopLeftRadius = borderRadius;
            imageElement.style.borderTopRightRadius = borderRadius;
            imageElement.style.borderBottomLeftRadius = borderRadius;
            imageElement.style.borderBottomRightRadius = borderRadius;
        }

        return imageElement;
    }

    private VisualElement CreateVectorElement(JObject nodeData, VisualElement parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Vector";

        VisualElement vectorElement = new VisualElement();
        vectorElement.AddToClassList("figma-vector");

        // Apply fills
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
            ApplyFills(fills, vectorElement);
        }

        return vectorElement;
    }

    private VisualElement CreateGenericElement(JObject nodeData, VisualElement parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "GenericElement";

        VisualElement genericElement = new VisualElement();
        genericElement.AddToClassList("figma-generic");

        return genericElement;
    }

    private void ApplyTransform(JObject nodeData, VisualElement element)
    {
        // Get bounding box
        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        if (boundingBox != null)
        {
            float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
            float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
            float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

            // Store original Figma position before scaling
            figmaPositions[element] = new Vector2(x, y);

            // Apply scale factor
            x *= scaleFactor;
            y *= scaleFactor;
            width *= scaleFactor;
            height *= scaleFactor;

            // Set size
            element.style.width = width;
            element.style.height = height;

            // Calculate position based on parent
            Vector2 position = CalculateRelativePosition(element, x, y, width, height);

            // Set position using absolute positioning
            element.style.position = Position.Absolute;
            element.style.left = position.x;
            element.style.top = position.y;
        }
    }

    private Vector2 CalculateRelativePosition(VisualElement element, float x, float y, float width, float height)
    {
        // Check if this is a root element
        if (element.parent == rootVisualElement || element.parent == null)
        {
            // For root elements, use absolute positioning from top-left
            return new Vector2(x, y);
        }
        else
        {
            // For nested elements, calculate relative to parent's Figma position
            VisualElement parentElement = element.parent;

            if (figmaPositions.ContainsKey(parentElement))
            {
                Vector2 parentFigmaPos = figmaPositions[parentElement];

                // Calculate relative position in Figma coordinates (unscaled)
                float relativeX = (x / scaleFactor) - parentFigmaPos.x;
                float relativeY = (y / scaleFactor) - parentFigmaPos.y;

                // Apply scale factor
                relativeX *= scaleFactor;
                relativeY *= scaleFactor;

                return new Vector2(relativeX, relativeY);
            }
            else
            {
                // Fallback: use absolute position
                if (enableDebugLogs)
                    Debug.LogWarning($"Parent Figma position not found for {element.name}, using absolute positioning");

                return new Vector2(x, y);
            }
        }
    }

    private void ApplyVisibility(JObject nodeData, VisualElement element)
    {
        bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ApplyFills(JArray fills, VisualElement element)
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

                Color backgroundColor = new Color(r, g, b, a);
                element.style.backgroundColor = backgroundColor;
            }
        }
        else if (fillType == "GRADIENT_LINEAR" || fillType == "GRADIENT_RADIAL")
        {
            // UI Toolkit doesn't directly support gradients in code, but you can use USS
            // For now, we'll use the first gradient stop color
            JArray gradientStops = firstFill["gradientStops"] as JArray;
            if (gradientStops != null && gradientStops.Count > 0)
            {
                JObject firstStop = gradientStops[0] as JObject;
                JObject colorObj = firstStop?["color"] as JObject;
                if (colorObj != null)
                {
                    float r = colorObj["r"]?.ToObject<float>() ?? 1f;
                    float g = colorObj["g"]?.ToObject<float>() ?? 1f;
                    float b = colorObj["b"]?.ToObject<float>() ?? 1f;
                    float a = firstFill["opacity"]?.ToObject<float>() ?? 1f;

                    Color backgroundColor = new Color(r, g, b, a);
                    element.style.backgroundColor = backgroundColor;
                }
            }
        }
    }

    private void ApplyTextFills(JArray fills, Label textElement)
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

                textElement.style.color = new Color(r, g, b, a);
            }
        }
    }

    private void ApplyTextStyling(JObject nodeData, Label textElement)
    {
        JObject style = nodeData["style"] as JObject;
        if (style == null) return;

        // Font size
        float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
        textElement.style.fontSize = fontSize * scaleFactor;

        // Text alignment
        string textAlignHorizontal = style["textAlignHorizontal"]?.ToString();
        string textAlignVertical = style["textAlignVertical"]?.ToString();

        // Horizontal alignment
        switch (textAlignHorizontal?.ToUpper())
        {
            case "CENTER":
                textElement.style.unityTextAlign = TextAnchor.MiddleCenter;
                break;
            case "RIGHT":
                textElement.style.unityTextAlign = TextAnchor.MiddleRight;
                break;
            case "JUSTIFIED":
                textElement.style.unityTextAlign = TextAnchor.MiddleLeft; // UI Toolkit doesn't have justified
                break;
            default:
                textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
                break;
        }

        // Adjust for vertical alignment (UI Toolkit combines both in TextAnchor)
        if (textAlignVertical?.ToUpper() == "TOP")
        {
            switch (textAlignHorizontal?.ToUpper())
            {
                case "CENTER":
                    textElement.style.unityTextAlign = TextAnchor.UpperCenter;
                    break;
                case "RIGHT":
                    textElement.style.unityTextAlign = TextAnchor.UpperRight;
                    break;
                default:
                    textElement.style.unityTextAlign = TextAnchor.UpperLeft;
                    break;
            }
        }
        else if (textAlignVertical?.ToUpper() == "BOTTOM")
        {
            switch (textAlignHorizontal?.ToUpper())
            {
                case "CENTER":
                    textElement.style.unityTextAlign = TextAnchor.LowerCenter;
                    break;
                case "RIGHT":
                    textElement.style.unityTextAlign = TextAnchor.LowerRight;
                    break;
                default:
                    textElement.style.unityTextAlign = TextAnchor.LowerLeft;
                    break;
            }
        }

        // Font weight
        float fontWeight = style["fontWeight"]?.ToObject<float>() ?? 400f;
        if (fontWeight >= 700)
        {
            textElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else if (fontWeight <= 300)
        {
            // UI Toolkit doesn't have a light font style, so we'll use normal
            textElement.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
        else
        {
            textElement.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        // Text wrapping
        textElement.style.whiteSpace = WhiteSpace.NoWrap; // Default to no wrap like Figma
    }

    private void ApplyCornerRadius(JObject nodeData, VisualElement element)
    {
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;

        if (cornerRadius > 0)
        {
            var borderRadius = new StyleLength(cornerRadius * scaleFactor);
            element.style.borderTopLeftRadius = borderRadius;
            element.style.borderTopRightRadius = borderRadius;
            element.style.borderBottomLeftRadius = borderRadius;
            element.style.borderBottomRightRadius = borderRadius;
        }

        // Handle individual corner radii
        JArray rectangleCornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (rectangleCornerRadii != null && rectangleCornerRadii.Count >= 4)
        {
            float topLeft = rectangleCornerRadii[0]?.ToObject<float>() ?? 0f;
            float topRight = rectangleCornerRadii[1]?.ToObject<float>() ?? 0f;
            float bottomRight = rectangleCornerRadii[2]?.ToObject<float>() ?? 0f;
            float bottomLeft = rectangleCornerRadii[3]?.ToObject<float>() ?? 0f;

            element.style.borderTopLeftRadius = topLeft * scaleFactor;
            element.style.borderTopRightRadius = topRight * scaleFactor;
            element.style.borderBottomRightRadius = bottomRight * scaleFactor;
            element.style.borderBottomLeftRadius = bottomLeft * scaleFactor;
        }
    }

    [ContextMenu("Clear Created UI")]
    public void ClearCreatedUI()
    {
        if (rootVisualElement != null)
        {
            rootVisualElement.Clear();
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
        Debug.Log("=== FigmaNodeDataConverterUIToolkit Setup Validation ===");

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

        // Check UI Document
        if (targetUIDocument == null && !createNewUIDocument)
        {
            Debug.LogError("❌ No target UI document and createNewUIDocument is disabled");
        }
        else
        {
            Debug.Log("✅ UI Document setup is valid");
        }

        // Check Font
        if (defaultFontDefinition.fontAsset == null)
        {
            Debug.LogWarning("⚠️ Default font not set - will use default system font");
        }
        else
        {
            Debug.Log($"✅ Default font configured: {defaultFontDefinition.fontAsset.name}");
        }

        // Check Style Sheet
        if (defaultStyleSheet == null)
        {
            Debug.LogWarning("⚠️ Default style sheet not set - consider adding one for better styling");
        }
        else
        {
            Debug.Log($"✅ Default style sheet configured: {defaultStyleSheet.name}");
        }

        // Check UXML Generation Settings
        if (generateUXMLFile)
        {
            if (string.IsNullOrEmpty(uxmlOutputPath))
            {
                Debug.LogError("❌ UXML output path is not set");
            }
            else
            {
                Debug.Log($"✅ UXML output path: {uxmlOutputPath}");
            }

            if (string.IsNullOrEmpty(uxmlFileName))
            {
                Debug.LogError("❌ UXML file name is not set");
            }
            else
            {
                Debug.Log($"✅ UXML file name: {uxmlFileName}.uxml");
            }
        }

        if (generateUSSFile)
        {
            if (string.IsNullOrEmpty(ussOutputPath))
            {
                Debug.LogError("❌ USS output path is not set");
            }
            else
            {
                Debug.Log($"✅ USS output path: {ussOutputPath}");
            }

            if (string.IsNullOrEmpty(ussFileName))
            {
                Debug.LogError("❌ USS file name is not set");
            }
            else
            {
                Debug.Log($"✅ USS file name: {ussFileName}.uss");
            }
        }

        Debug.Log("=== Validation Complete ===");
    }

    [ContextMenu("Debug Positioning")]
    public void DebugPositioning()
    {
        Debug.Log("=== Position Debug Information (UI Toolkit) ===");

        foreach (var kvp in createdNodes)
        {
            if (kvp.Value != null)
            {
                VisualElement element = kvp.Value;
                Vector2 figmaPos = figmaPositions.ContainsKey(element) ? figmaPositions[element] : Vector2.zero;
                var resolvedStyle = element.resolvedStyle;

                Debug.Log($"Element: {element.name}");
                Debug.Log($"  - Figma Position: {figmaPos}");
                Debug.Log($"  - UI Toolkit Position: left={element.style.left.value}, top={element.style.top.value}");
                Debug.Log($"  - Resolved Position: ({resolvedStyle.left}, {resolvedStyle.top})");
                Debug.Log($"  - Size: width={element.style.width.value}, height={element.style.height.value}");
                Debug.Log($"  - Resolved Size: ({resolvedStyle.width}, {resolvedStyle.height})");
                Debug.Log($"  - Position Type: {element.style.position.value}");
                Debug.Log($"  - Parent: {(element.parent != null ? element.parent.name : "None")}");
                Debug.Log("---");
            }
        }

        Debug.Log("=== End Position Debug ===");
    }

    [ContextMenu("Generate USS Classes")]
    public void GenerateUSSClasses()
    {
        Debug.Log("=== Generated USS Classes for Figma Elements ===");
        Debug.Log("/* Base Figma Element Styles */");
        Debug.Log(".figma-element {");
        Debug.Log("    position: absolute;");
        Debug.Log("}");
        Debug.Log("");
        Debug.Log("/* Container Styles */");
        Debug.Log(".figma-container {");
        Debug.Log("    overflow: hidden;");
        Debug.Log("}");
        Debug.Log("");
        Debug.Log("/* Text Styles */");
        Debug.Log(".figma-text {");
        Debug.Log("    white-space: nowrap;");
        Debug.Log("}");
        Debug.Log("=== End USS Classes ===");
    }

    [ContextMenu("Preview Generated UXML")]
    public void PreviewGeneratedUXML()
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

        StartCoroutine(PreviewUXMLCoroutine());
    }

    private IEnumerator PreviewUXMLCoroutine()
    {
        JObject documentNode = nodeDataAsset.GetDocumentNode(targetNodeId);
        if (documentNode == null)
        {
            Debug.LogError($"Could not find document node for ID: {targetNodeId}");
            yield break;
        }

        // Initialize generation
        InitializeUXMLGeneration();

        try
        {
            StartUXMLDocument();
            ProcessFigmaNodeForUXML(documentNode);
            EndUXMLDocument();

            Debug.Log("=== Generated UXML Preview ===");
            Debug.Log(uxmlBuilder.ToString());
            Debug.Log("=== Generated USS Preview ===");
            Debug.Log(ussBuilder.ToString());
            Debug.Log("=== End Preview ===");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during UXML preview: {ex.Message}");
        }

        yield return null;
    }

    #region UXML Generation Methods

    private void InitializeUXMLGeneration()
    {
        uxmlBuilder.Clear();
        ussBuilder.Clear();
        indentLevel = 0;
        figmaAbsolutePositions.Clear();
        parentNodeStack.Clear();

        // Initialize USS with base styles
        ussBuilder.AppendLine("/* Generated USS from Figma */");
        ussBuilder.AppendLine();
        ussBuilder.AppendLine(".figma-element {");
        ussBuilder.AppendLine("    position: absolute;");
        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }

    private void StartUXMLDocument()
    {
        uxmlBuilder.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\" editor-extension-mode=\"False\">");
        indentLevel++;

        // Add stylesheet reference if generating USS
        if (generateUSSFile)
        {
            AddIndentedLine($"<Style src=\"project://database/{ussOutputPath}{ussFileName}.uss?fileID=7433441132597879392&amp;guid=yourguidhere&amp;type=3#GeneratedStyles\" />");
        }
    }

    private void EndUXMLDocument()
    {
        indentLevel--;
        uxmlBuilder.AppendLine("</ui:UXML>");
    }

    private void ProcessFigmaNodeForUXML(JObject nodeData)
    {
        if (nodeData == null) return;

        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        // Generate fallback ID if null
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = System.Guid.NewGuid().ToString();
        }

        if (enableDebugLogs)
            Debug.Log($"Generating UXML for node: {nodeName} (ID: {nodeId}, Type: {nodeType})");

        // Check if this node has children
        bool hasChildren = nodeData["children"] is JArray children && children.Count > 0;

        // Generate UXML element based on node type
        switch (nodeType?.ToUpper())
        {
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
                GenerateContainerUXML(nodeData, hasChildren);
                break;

            case "TEXT":
                GenerateTextUXML(nodeData, hasChildren);
                break;

            case "RECTANGLE":
            case "ELLIPSE":
            case "ROUNDED_RECTANGLE":
                GenerateImageUXML(nodeData, hasChildren);
                break;

            case "VECTOR":
            case "STAR":
            case "POLYGON":
            case "BOOLEAN_OPERATION":
                GenerateVectorUXML(nodeData, hasChildren);
                break;

            default:
                GenerateGenericUXML(nodeData, hasChildren);
                break;
        }

        // Process children if they exist
        if (hasChildren)
        {
            // Push current node as parent for children
            parentNodeStack.Push(nodeId);
            indentLevel++;

            JArray childrenArray = nodeData["children"] as JArray;
            foreach (JObject child in childrenArray)
            {
                ProcessFigmaNodeForUXML(child);
            }

            indentLevel--;
            parentNodeStack.Pop(); // Remove current node from parent stack

            // Close the parent element
            CloseUXMLElement(nodeType);
        }
    }

    private void GenerateContainerUXML(JObject nodeData, bool hasChildren)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "Container");
        string className = GenerateClassName(nodeData);

        if (hasChildren)
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\">");
        }
        else
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\" />");
        }

        // Generate USS for this container
        GenerateContainerUSS(nodeData, className);
    }

    private void GenerateTextUXML(JObject nodeData, bool hasChildren)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "Text");
        string className = GenerateClassName(nodeData);
        string characters = nodeData["characters"]?.ToString() ?? "Sample Text";

        if (hasChildren)
        {
            AddIndentedLine($"<ui:Label name=\"{nodeName}\" text=\"{EscapeXMLString(characters)}\" class=\"{className}\">");
        }
        else
        {
            AddIndentedLine($"<ui:Label name=\"{nodeName}\" text=\"{EscapeXMLString(characters)}\" class=\"{className}\" />");
        }

        // Generate USS for this text element
        GenerateTextUSS(nodeData, className);
    }

    private void GenerateImageUXML(JObject nodeData, bool hasChildren)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "Image");
        string className = GenerateClassName(nodeData);

        if (hasChildren)
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\">");
        }
        else
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\" />");
        }

        // Generate USS for this image element
        GenerateImageUSS(nodeData, className);
    }

    private void GenerateVectorUXML(JObject nodeData, bool hasChildren)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "Vector");
        string className = GenerateClassName(nodeData);

        if (hasChildren)
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\">");
        }
        else
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\" />");
        }

        // Generate USS for this vector element
        GenerateVectorUSS(nodeData, className);
    }

    private void GenerateGenericUXML(JObject nodeData, bool hasChildren)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "Element");
        string className = GenerateClassName(nodeData);

        if (hasChildren)
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\">");
        }
        else
        {
            AddIndentedLine($"<ui:VisualElement name=\"{nodeName}\" class=\"{className}\" />");
        }

        // Generate USS for this generic element
        GenerateGenericUSS(nodeData, className);
    }

    private void GenerateContainerUSS(JObject nodeData, string className)
    {
        ussBuilder.AppendLine($".{className} {{");

        // Apply transform properties
        ApplyTransformUSS(nodeData);

        // Apply background fills
        ApplyFillsUSS(nodeData);

        // Apply corner radius
        ApplyCornerRadiusUSS(nodeData);

        // Apply visibility
        ApplyVisibilityUSS(nodeData);

        // Container-specific properties
        ussBuilder.AppendLine("    overflow: hidden;");

        // For containers, ensure they can contain children properly
        if (nodeData["children"] is JArray children && children.Count > 0)
        {
            ussBuilder.AppendLine("    /* Container with children */");
        }

        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }
    private void GenerateTextUSS(JObject nodeData, string className)
    {
        ussBuilder.AppendLine($".{className} {{");

        // Apply transform properties
        ApplyTransformUSS(nodeData);

        // Apply text styling
        ApplyTextStylingUSS(nodeData);

        // Apply text fills for color
        ApplyTextFillsUSS(nodeData);

        // Apply visibility
        ApplyVisibilityUSS(nodeData);

        ussBuilder.AppendLine("    white-space: nowrap;");
        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }

    private void GenerateImageUSS(JObject nodeData, string className)
    {
        ussBuilder.AppendLine($".{className} {{");

        // Apply transform properties
        ApplyTransformUSS(nodeData);

        // Apply fills for background
        ApplyFillsUSS(nodeData);

        // Apply corner radius
        ApplyCornerRadiusUSS(nodeData);

        // Apply visibility
        ApplyVisibilityUSS(nodeData);

        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }

    private void GenerateVectorUSS(JObject nodeData, string className)
    {
        ussBuilder.AppendLine($".{className} {{");

        // Apply transform properties
        ApplyTransformUSS(nodeData);

        // Apply fills
        ApplyFillsUSS(nodeData);

        // Apply corner radius
        ApplyCornerRadiusUSS(nodeData);

        // Apply visibility
        ApplyVisibilityUSS(nodeData);

        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }

    private void GenerateGenericUSS(JObject nodeData, string className)
    {
        ussBuilder.AppendLine($".{className} {{");

        // Apply transform properties
        ApplyTransformUSS(nodeData);

        // Apply background fills
        ApplyFillsUSS(nodeData);

        // Apply corner radius
        ApplyCornerRadiusUSS(nodeData);

        // Apply visibility
        ApplyVisibilityUSS(nodeData);

        ussBuilder.AppendLine("}");
        ussBuilder.AppendLine();
    }

    private void ApplyTransformUSS(JObject nodeData)
    {
        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        if (boundingBox != null)
        {
            float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
            float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
            float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

            string nodeId = nodeData["id"]?.ToString();
            if (!string.IsNullOrEmpty(nodeId))
            {
                figmaAbsolutePositions[nodeId] = new Vector2(x, y);
            }

            // Calculate relative position based on parent
            Vector2 relativePosition = CalculateRelativePositionForUSS(nodeData, x, y);

            // Apply scale factor
            width *= scaleFactor;
            height *= scaleFactor;
            relativePosition.x *= scaleFactor;
            relativePosition.y *= scaleFactor;

            ussBuilder.AppendLine($"    width: {width}px;");
            ussBuilder.AppendLine($"    height: {height}px;");
            ussBuilder.AppendLine($"    left: {relativePosition.x}px;");
            ussBuilder.AppendLine($"    top: {relativePosition.y}px;");
            ussBuilder.AppendLine("    position: absolute;");
        }
    }

    private void ApplyFillsUSS(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
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

                    ussBuilder.AppendLine($"    background-color: rgba({r * 255:F0}, {g * 255:F0}, {b * 255:F0}, {a});");
                }
            }
        }
    }

    private void ApplyTextFillsUSS(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null && fills.Count > 0)
        {
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

                    ussBuilder.AppendLine($"    color: rgba({r * 255:F0}, {g * 255:F0}, {b * 255:F0}, {a});");
                }
            }
        }
        else
        {
            // Use default text color
            ussBuilder.AppendLine($"    color: rgba({defaultTextColor.r * 255:F0}, {defaultTextColor.g * 255:F0}, {defaultTextColor.b * 255:F0}, {defaultTextColor.a});");
        }
    }

    private void ApplyTextStylingUSS(JObject nodeData)
    {
        JObject style = nodeData["style"] as JObject;
        if (style == null) return;

        // Font size
        float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
        ussBuilder.AppendLine($"    font-size: {fontSize * scaleFactor}px;");

        // Text alignment
        string textAlignHorizontal = style["textAlignHorizontal"]?.ToString();
        switch (textAlignHorizontal?.ToUpper())
        {
            case "CENTER":
                ussBuilder.AppendLine("    -unity-text-align: middle-center;");
                break;
            case "RIGHT":
                ussBuilder.AppendLine("    -unity-text-align: middle-right;");
                break;
            case "JUSTIFIED":
                ussBuilder.AppendLine("    -unity-text-align: middle-left;");
                break;
            default:
                ussBuilder.AppendLine("    -unity-text-align: middle-left;");
                break;
        }

        // Font weight
        float fontWeight = style["fontWeight"]?.ToObject<float>() ?? 400f;
        if (fontWeight >= 700)
        {
            ussBuilder.AppendLine("    -unity-font-style: bold;");
        }
    }

    private void ApplyCornerRadiusUSS(JObject nodeData)
    {
        float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;

        if (cornerRadius > 0)
        {
            float scaledRadius = cornerRadius * scaleFactor;
            ussBuilder.AppendLine($"    border-top-left-radius: {scaledRadius}px;");
            ussBuilder.AppendLine($"    border-top-right-radius: {scaledRadius}px;");
            ussBuilder.AppendLine($"    border-bottom-left-radius: {scaledRadius}px;");
            ussBuilder.AppendLine($"    border-bottom-right-radius: {scaledRadius}px;");
        }

        // Handle individual corner radii
        JArray rectangleCornerRadii = nodeData["rectangleCornerRadii"] as JArray;
        if (rectangleCornerRadii != null && rectangleCornerRadii.Count >= 4)
        {
            float topLeft = rectangleCornerRadii[0]?.ToObject<float>() ?? 0f;
            float topRight = rectangleCornerRadii[1]?.ToObject<float>() ?? 0f;
            float bottomRight = rectangleCornerRadii[2]?.ToObject<float>() ?? 0f;
            float bottomLeft = rectangleCornerRadii[3]?.ToObject<float>() ?? 0f;

            ussBuilder.AppendLine($"    border-top-left-radius: {topLeft * scaleFactor}px;");
            ussBuilder.AppendLine($"    border-top-right-radius: {topRight * scaleFactor}px;");
            ussBuilder.AppendLine($"    border-bottom-right-radius: {bottomRight * scaleFactor}px;");
            ussBuilder.AppendLine($"    border-bottom-left-radius: {bottomLeft * scaleFactor}px;");
        }

        // Handle ellipse shapes
        string nodeType = nodeData["type"]?.ToString();
        if (nodeType == "ELLIPSE")
        {
            ussBuilder.AppendLine("    border-top-left-radius: 50%;");
            ussBuilder.AppendLine("    border-top-right-radius: 50%;");
            ussBuilder.AppendLine("    border-bottom-left-radius: 50%;");
            ussBuilder.AppendLine("    border-bottom-right-radius: 50%;");
        }
    }

    private void ApplyVisibilityUSS(JObject nodeData)
    {
        bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
        if (!visible)
        {
            ussBuilder.AppendLine("    display: none;");
        }
    }

    private IEnumerator SaveGeneratedFiles()
    {
#if UNITY_EDITOR
        // Ensure output directories exist
        if (!Directory.Exists(uxmlOutputPath))
        {
            Directory.CreateDirectory(uxmlOutputPath);
        }

        if (generateUSSFile && !Directory.Exists(ussOutputPath))
        {
            Directory.CreateDirectory(ussOutputPath);
        }

        // Save UXML file
        string uxmlFilePath = Path.Combine(uxmlOutputPath, $"{uxmlFileName}.uxml");
        File.WriteAllText(uxmlFilePath, uxmlBuilder.ToString());

        if (enableDebugLogs)
            Debug.Log($"✓ UXML file saved: {uxmlFilePath}");

        // Save USS file if enabled
        if (generateUSSFile)
        {
            string ussFilePath = Path.Combine(ussOutputPath, $"{ussFileName}.uss");
            File.WriteAllText(ussFilePath, ussBuilder.ToString());

            if (enableDebugLogs)
                Debug.Log($"✓ USS file saved: {ussFilePath}");
        }

        // Refresh the asset database
        AssetDatabase.Refresh();

        yield return null;
#else
        Debug.LogWarning("UXML/USS file generation is only available in the Unity Editor.");
        yield return null;
#endif
    }

    private string GetSanitizedName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Element";

        // Replace spaces and special characters with underscores
        return System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
    }

    private string GenerateClassName(JObject nodeData)
    {
        string nodeName = GetSanitizedName(nodeData["name"]?.ToString() ?? "element");
        string nodeType = nodeData["type"]?.ToString()?.ToLower() ?? "generic";
        string nodeId = nodeData["id"]?.ToString();

        // Create a unique but readable class name
        string className = $"figma-{nodeType}-{nodeName}";

        // Add a hash of the node ID for uniqueness if needed
        if (!string.IsNullOrEmpty(nodeId))
        {
            int hash = nodeId.GetHashCode();
            className += $"-{Mathf.Abs(hash).ToString().Substring(0, Mathf.Min(4, Mathf.Abs(hash).ToString().Length))}";
        }

        return className;
    }

    private string EscapeXMLString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace("&", "&amp;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("\"", "&quot;")
                  .Replace("'", "&apos;");
    }

    private void AddIndentedLine(string line)
    {
        string indent = new string(' ', indentLevel * 4);
        uxmlBuilder.AppendLine(indent + line);
    }

    private void CloseUXMLElement(string nodeType)
    {
        switch (nodeType?.ToUpper())
        {
            case "TEXT":
                AddIndentedLine("</ui:Label>");
                break;
            case "FRAME":
            case "GROUP":
            case "COMPONENT":
            case "INSTANCE":
            case "RECTANGLE":
            case "ELLIPSE":
            case "ROUNDED_RECTANGLE":
            case "VECTOR":
            case "STAR":
            case "POLYGON":
            case "BOOLEAN_OPERATION":
            default:
                AddIndentedLine("</ui:VisualElement>");
                break;
        }
    }

    private Vector2 CalculateRelativePositionForUSS(JObject nodeData, float absoluteX, float absoluteY)
    {
        // For root elements, use absolute positioning
        if (parentNodeStack.Count == 0)
        {
            return new Vector2(absoluteX, absoluteY);
        }

        // Get current parent from stack
        string parentId = parentNodeStack.Peek();
        if (!string.IsNullOrEmpty(parentId) && figmaAbsolutePositions.ContainsKey(parentId))
        {
            Vector2 parentPos = figmaAbsolutePositions[parentId];
            return new Vector2(absoluteX - parentPos.x, absoluteY - parentPos.y);
        }

        // Fallback to absolute positioning
        return new Vector2(absoluteX, absoluteY);
    }

    #endregion
}
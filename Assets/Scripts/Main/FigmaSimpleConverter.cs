using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Complete Figma converter that works without FigmaNodeDataAsset
/// Supports URL input, flexible image download options, and full UI conversion
/// </summary>
public class FigmaSimpleConverter : MonoBehaviour, IFigmaNodeConverter
{
    [Header("Figma API Settings")]
    public string figmaToken = "YOUR_FIGMA_TOKEN";
    
    [Header("Figma URL Input")]
    [Tooltip("Paste your Figma URL here to auto-extract file ID and node ID")]
    public string figmaUrl = "";
    
    [Header("Extracted IDs (Auto-filled from URL)")]
    [SerializeField] private string fileId = "YOUR_FILE_ID";
    [SerializeField] private string nodeId = "YOUR_NODE_ID";

    [Header("UI Settings")]
    public Canvas targetCanvas;
    public bool createNewCanvas = true;
    public string canvasName = "FigmaUI_Canvas";
    public TMP_FontAsset defaultFont;
    public Color defaultTextColor = Color.black;
    public float scaleFactor = 1f;

    [Header("Image Download Options")]
    [Tooltip("If true, only downloads images from the target node itself, not its children")]
    public bool downloadOnlyTargetNode = false;

    [Tooltip("If true, downloads images recursively from all children")]
    public bool downloadChildrenImages = true;

    [Tooltip("If true, downloads images at all")]
    public bool downloadImages = true;

    public string imageFormat = "png";
    public float imageScale = 1f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private Dictionary<string, GameObject> createdNodes = new Dictionary<string, GameObject>();
    private Dictionary<GameObject, Vector2> figmaPositions = new Dictionary<GameObject, Vector2>();
    private JObject _currentNodeData;

    [ContextMenu("Extract IDs from URL")]
    public void ExtractIdsFromUrl()
    {
        if (string.IsNullOrEmpty(figmaUrl))
        {
            Debug.LogError("Figma URL is empty! Please paste a Figma URL first.");
            return;
        }

        var extractedIds = ExtractFileAndNodeIds(figmaUrl);
        if (extractedIds != null)
        {
            fileId = extractedIds.Value.fileId;
            nodeId = extractedIds.Value.nodeId;
            
            if (enableDebugLogs)
            {
                Debug.Log($"✓ Extracted from URL:");
                Debug.Log($"  File ID: {fileId}");
                Debug.Log($"  Node ID: {nodeId}");
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        else
        {
            Debug.LogError("Failed to extract file ID and node ID from URL. Please check the URL format.");
        }
    }

    [ContextMenu("Download and Convert Everything")]
    public void DownloadAndConvertEverything()
    {
        // Auto-extract IDs if not already set
        if ((string.IsNullOrEmpty(fileId) || fileId == "YOUR_FILE_ID") && !string.IsNullOrEmpty(figmaUrl))
        {
            ExtractIdsFromUrl();
        }
        
        StartCoroutine(DownloadAndConvertCoroutine());
    }

    [ContextMenu("Download and Convert (Target Node Only)")]
    public void DownloadAndConvertTargetOnly()
    {
        // Auto-extract IDs if not already set
        if ((string.IsNullOrEmpty(fileId) || fileId == "YOUR_FILE_ID") && !string.IsNullOrEmpty(figmaUrl))
        {
            ExtractIdsFromUrl();
        }
        
        downloadOnlyTargetNode = true;
        downloadChildrenImages = false;
        StartCoroutine(DownloadAndConvertCoroutine());
    }

    [ContextMenu("Convert Without Downloading Images")]
    public void ConvertWithoutImages()
    {
        // Auto-extract IDs if not already set
        if ((string.IsNullOrEmpty(fileId) || fileId == "YOUR_FILE_ID") && !string.IsNullOrEmpty(figmaUrl))
        {
            ExtractIdsFromUrl();
        }
        
        downloadImages = false;
        StartCoroutine(DownloadAndConvertCoroutine());
    }

    /// <summary>
    /// Extracts file ID and node ID from a Figma URL
    /// Supports formats like:
    /// - https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15&m=dev
    /// - https://www.figma.com/file/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15
    /// - https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled
    /// </summary>
    private (string fileId, string nodeId)? ExtractFileAndNodeIds(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            // Remove any whitespace
            url = url.Trim();

            // Check if it's a valid Figma URL
            if (!url.Contains("figma.com"))
            {
                Debug.LogError("Invalid Figma URL. URL must contain 'figma.com'");
                return null;
            }

            string extractedFileId = "";
            string extractedNodeId = "";

            // Extract file ID from URL path
            // Pattern: /design/FILE_ID/ or /file/FILE_ID/
            var fileIdMatch = System.Text.RegularExpressions.Regex.Match(url, @"/(?:design|file)/([A-Za-z0-9]+)/");
            if (fileIdMatch.Success)
            {
                extractedFileId = fileIdMatch.Groups[1].Value;
            }
            else
            {
                Debug.LogError("Could not extract file ID from URL. Expected format: /design/FILE_ID/ or /file/FILE_ID/");
                return null;
            }

            // Extract node ID from query parameters
            // Pattern: node-id=1001-15 or node-id=1001%3A15 (URL encoded colon)
            var nodeIdMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]node-id=([^&]+)");
            if (nodeIdMatch.Success)
            {
                extractedNodeId = nodeIdMatch.Groups[1].Value;
                // Convert URL encoded colon (%3A) back to colon
                extractedNodeId = extractedNodeId.Replace("%3A", ":");
                // Convert dash to colon if it's in the format 1001-15
                if (extractedNodeId.Contains("-") && !extractedNodeId.Contains(":"))
                {
                    extractedNodeId = extractedNodeId.Replace("-", ":");
                }
            }
            else
            {
                Debug.LogWarning("No node ID found in URL. You may need to specify a specific node to convert.");
                // For some cases, you might want to use the root node or a default
                // For now, we'll return null to indicate no node ID was found
                return null;
            }

            if (string.IsNullOrEmpty(extractedFileId))
            {
                Debug.LogError("Failed to extract file ID from URL");
                return null;
            }

            return (extractedFileId, extractedNodeId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing Figma URL: {ex.Message}");
            return null;
        }
    }

    private IEnumerator DownloadAndConvertCoroutine()
    {
        if (enableDebugLogs)
        {
            string mode =
                downloadOnlyTargetNode ? "target node only"
                : downloadChildrenImages ? "with children"
                : "without images";
            Debug.Log($"Starting download and convert in '{mode}' mode");
        }

        // Download node data
        yield return DownloadNodeData();

        // Download images if enabled
        if (downloadImages)
        {
            yield return DownloadImages();
        }

        // Convert to UI
        ConvertNodeToUI();
    }

    private IEnumerator DownloadNodeData()
    {
        string encodedNodeId = UnityEngine.Networking.UnityWebRequest.EscapeURL(nodeId);
        string url = $"https://api.figma.com/v1/files/{fileId}/nodes?ids={encodedNodeId}";

        using (
            UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(
                url
            )
        )
        {
            www.SetRequestHeader("X-FIGMA-TOKEN", figmaToken);
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string jsonContent = www.downloadHandler.text;
                JObject root = JObject.Parse(jsonContent);
                _currentNodeData = root["nodes"]?[nodeId]?["document"] as JObject;

                if (_currentNodeData != null)
                {
                    if (enableDebugLogs)
                        Debug.Log(
                            $"✓ Downloaded node data: {_currentNodeData["name"]} (Type: {_currentNodeData["type"]})"
                        );
                }
                else
                {
                    Debug.LogError($"Could not find document node for ID: {nodeId}");
                }
            }
            else
            {
                Debug.LogError($"✗ Failed to download node: {www.error}");
            }
        }
    }

    private IEnumerator DownloadImages()
    {
        if (_currentNodeData == null)
        {
            Debug.LogError("No node data available for image download");
            yield break;
        }

        List<string> imageNodeIds = new List<string>();

        if (downloadOnlyTargetNode)
        {
            // Only check the target node itself
            CheckNodeForImages(_currentNodeData, imageNodeIds);
            if (enableDebugLogs)
                Debug.Log($"Checking target node only for images. Found: {imageNodeIds.Count}");
        }
        else if (downloadChildrenImages)
        {
            // Check all children recursively
            CollectImageNodeIds(_currentNodeData, imageNodeIds);
            if (enableDebugLogs)
                Debug.Log($"Checking all children for images. Found: {imageNodeIds.Count}");
        }

        if (imageNodeIds.Count == 0)
        {
            if (enableDebugLogs)
                Debug.Log("No image nodes found to download.");
            yield break;
        }

        // Download images
        yield return DownloadImagesFromIds(imageNodeIds);
    }

    private void CheckNodeForImages(JObject nodeData, List<string> imageNodeIds)
    {
        string nodeName = nodeData["name"]?.ToString();
        string nodeId = nodeData["id"]?.ToString();

        if (
            !string.IsNullOrEmpty(nodeName)
            && !string.IsNullOrEmpty(nodeId)
            && nodeName.StartsWith(Constant.IMAGE_PREFIX)
            && !imageNodeIds.Contains(nodeId)
        )
        {
            imageNodeIds.Add(nodeId);
            if (enableDebugLogs)
                Debug.Log($"Found image in target node: {nodeName} ({nodeId})");
        }
    }

    private void CollectImageNodeIds(JToken token, List<string> imageNodeIds)
    {
        if (token == null)
            return;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();
            string nodeName = obj["name"]?.ToString();

            if (
                !string.IsNullOrEmpty(nodeName)
                && !string.IsNullOrEmpty(nodeId)
                && nodeName.StartsWith(Constant.IMAGE_PREFIX)
                && !imageNodeIds.Contains(nodeId)
            )
            {
                imageNodeIds.Add(nodeId);
                if (enableDebugLogs)
                    Debug.Log($"Found image node: {nodeName} ({nodeId})");
            }

            // Recursively check children
            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                CollectImageNodeIds(childrenToken, imageNodeIds);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
                CollectImageNodeIds(child, imageNodeIds);
        }
    }

    private IEnumerator DownloadImagesFromIds(List<string> imageNodeIds)
    {
        var figmaApi = new FigmaApi(figmaToken);
        var imageRequest = new ImageRequest(fileId)
        {
            ids = imageNodeIds.ToArray(),
            format = imageFormat,
            scale = imageScale,
            useAbsoluteBounds = true,
        };

        Task<Dictionary<string, byte[]>> task = figmaApi.GetImageAsync(
            imageRequest,
            CancellationToken.None
        );

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogError(
                $"✗ Failed to download images: {task.Exception?.GetBaseException().Message}"
            );
            yield break;
        }

        var images = task.Result;
        if (images == null || images.Count == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("No images were downloaded.");
            yield break;
        }

        // Save downloaded images
        string resourcesSpritesPath = Path.Combine(
            Application.dataPath,
            "Resources",
            Constant.SAVE_IMAGE_FOLDER,
            nodeId.Replace(":", "-")
        );

        EnsureDirectory(resourcesSpritesPath);

        foreach (var kvp in images)
        {
            string imageNodeId = kvp.Key;
            byte[] imageData = kvp.Value;

            if (imageData == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"⚠ Image node {imageNodeId} returned null data");
                continue;
            }

            string nodeName = FindNodeNameById(_currentNodeData, imageNodeId) ?? imageNodeId;
            string fileName = nodeName.SanitizeFileName();
            string filePath = Path.Combine(resourcesSpritesPath, $"{fileName}.{imageFormat}");

            File.WriteAllBytes(filePath, imageData);
            if (enableDebugLogs)
                Debug.Log($"✓ Saved image: {fileName}.{imageFormat}");
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        figmaApi.Dispose();
    }

    private string FindNodeNameById(JToken token, string targetId)
    {
        if (token == null)
            return null;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();

            if (nodeId == targetId)
            {
                return obj["name"]?.ToString();
            }

            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                string result = FindNodeNameById(childrenToken, targetId);
                if (result != null)
                    return result;
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
            {
                string result = FindNodeNameById(child, targetId);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    [ContextMenu("Convert to UI")]
    public void ConvertNodeToUI()
    {
        if (_currentNodeData == null)
        {
            Debug.LogError("No node data available. Please download data first.");
            return;
        }

        if (string.IsNullOrEmpty(nodeId))
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

        if (enableDebugLogs)
        {
            Debug.Log(
                $"Converting node: {_currentNodeData["name"]} (Type: {_currentNodeData["type"]})"
            );
        }

        try
        {
            ProcessFigmaNode(_currentNodeData, targetCanvas.transform);

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

    public void CreateCanvas()
    {
        GameObject canvasGO = new GameObject(canvasName);
        targetCanvas = canvasGO.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (enableDebugLogs)
            Debug.Log($"✓ Created Canvas: {canvasName}");
    }

    // Simplified ProcessFigmaNode - you can copy the full implementation from FigmaNodeDataConverter
    private GameObject ProcessFigmaNode(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

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

    // Add the rest of the conversion methods from FigmaNodeDataConverter here
    // (CreateContainerNode, CreateTextNode, CreateImageNode, etc.)
    // For brevity, I'm not copying all the methods, but they would be identical

    private GameObject CreateContainerNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Container";
        nodeName = nodeName.SanitizeFileName();

        GameObject container = new GameObject(nodeName);
        container.transform.SetParent(parent, false);

        RectTransform rectTransform = container.AddComponent<RectTransform>();

        JArray fills = nodeData["fills"] as JArray;
        bool hasFills = fills != null && fills.Count > 0;
        bool hasImagePrefix = nodeName.StartsWith(Constant.IMAGE_PREFIX);

        if (hasFills || hasImagePrefix)
        {
            Image backgroundImage = container.AddComponent<Image>();

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

        RectTransform rectTransform = textGO.AddComponent<RectTransform>();

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Image image = textGO.AddComponent<Image>();
            ApplyImageSprite(nodeName, image);

            if (enableDebugLogs)
                Debug.Log($"Text node '{nodeName}' converted to image due to prefix");
        }
        else
        {
            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();

            string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
            tmpText.text = characters;
            tmpText.textWrappingMode = TextWrappingModes.NoWrap;

            if (defaultFont != null)
                tmpText.font = defaultFont;

            ApplyTextStyling(nodeData, tmpText);

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

        RectTransform rectTransform = imageGO.AddComponent<RectTransform>();
        Image image = imageGO.AddComponent<Image>();

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(nodeName, image);
        }
        else
        {
            JArray fills = nodeData["fills"] as JArray;
            if (fills != null && fills.Count > 0)
            {
                ApplyFills(fills, image);
            }
        }

        return imageGO;
    }

    private GameObject CreateVectorNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Vector";
        nodeName = nodeName.SanitizeFileName();

        GameObject vectorGO = new GameObject(nodeName);
        vectorGO.transform.SetParent(parent, false);

        RectTransform rectTransform = vectorGO.AddComponent<RectTransform>();
        Image image = vectorGO.AddComponent<Image>();

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            ApplyImageSprite(nodeName, image);
        }
        else
        {
            JArray fills = nodeData["fills"] as JArray;
            if (fills != null && fills.Count > 0)
            {
                ApplyFills(fills, image);
            }
        }

        return vectorGO;
    }

    private GameObject CreateGenericNode(JObject nodeData, Transform parent)
    {
        string nodeName = nodeData["name"]?.ToString() ?? "GenericNode";
        nodeName = nodeName.SanitizeFileName();
        GameObject genericGO = new GameObject(nodeName);
        genericGO.transform.SetParent(parent, false);

        RectTransform rectTransform = genericGO.AddComponent<RectTransform>();

        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Image image = genericGO.AddComponent<Image>();
            ApplyImageSprite(nodeName, image);

            if (enableDebugLogs)
                Debug.Log($"Generic node '{nodeName}' converted to image due to prefix");
        }

        return genericGO;
    }

    private void ApplyImageSprite(string nodeName, Image image)
    {
        if (nodeName.StartsWith(Constant.IMAGE_PREFIX))
        {
            Sprite sprite = Resources.Load<Sprite>(
                $"Sprites/{nodeId.Replace(":", "-")}/{nodeName}"
            );
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

    private void ApplyTransform(JObject nodeData, GameObject gameObject)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        if (boundingBox != null)
        {
            float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
            float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
            float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
            float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

            figmaPositions[gameObject] = new Vector2(x, y);

            x *= scaleFactor;
            y *= scaleFactor;
            width *= scaleFactor;
            height *= scaleFactor;

            rectTransform.sizeDelta = new Vector2(width, height);

            Vector2 relativePosition = CalculateRelativePosition(
                rectTransform,
                x,
                y,
                width,
                height
            );
            rectTransform.anchoredPosition = relativePosition;

            SetAnchorsAndPivot(rectTransform);
        }
    }

    private Vector2 CalculateRelativePosition(
        RectTransform rectTransform,
        float x,
        float y,
        float width,
        float height
    )
    {
        if (rectTransform.parent == targetCanvas.transform)
        {
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            float unityX = x - (canvasWidth * 0.5f);
            float unityY = (canvasHeight * 0.5f) - y;

            return new Vector2(unityX, unityY);
        }
        else
        {
            GameObject parentGameObject = rectTransform.parent.gameObject;

            if (figmaPositions.ContainsKey(parentGameObject))
            {
                Vector2 parentFigmaPos = figmaPositions[parentGameObject];

                float relativeX = (x / scaleFactor) - parentFigmaPos.x;
                float relativeY = (y / scaleFactor) - parentFigmaPos.y;

                relativeX *= scaleFactor;
                relativeY *= scaleFactor;

                return new Vector2(relativeX, -relativeY);
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning(
                        $"Parent Figma position not found for {rectTransform.name}, using absolute positioning"
                    );

                return new Vector2(x, -y);
            }
        }
    }

    private void SetAnchorsAndPivot(RectTransform rectTransform)
    {
        if (rectTransform.parent == targetCanvas.transform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
        }
    }

    private void ApplyVisibility(JObject nodeData, GameObject gameObject)
    {
        bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
        gameObject.SetActive(visible);
    }

    private void ApplyFills(JArray fills, Image image)
    {
        if (fills == null || fills.Count == 0)
            return;

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null)
            return;

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
        if (fills == null || fills.Count == 0)
            return;

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null)
            return;

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
        if (style == null)
            return;

        float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
        tmpText.fontSize = fontSize * scaleFactor;

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

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        Debug.Log("=== FigmaSimpleConverter Setup Validation ===");

        if (string.IsNullOrEmpty(figmaToken) || figmaToken == "YOUR_FIGMA_TOKEN")
        {
            Debug.LogError("❌ Figma token not set");
        }
        else
        {
            Debug.Log("✅ Figma token configured");
        }

        // Check URL input
        if (string.IsNullOrEmpty(figmaUrl))
        {
            Debug.LogWarning("⚠️ Figma URL not set - you can paste a Figma URL to auto-extract IDs");
        }
        else
        {
            Debug.Log($"✅ Figma URL provided: {figmaUrl}");
            
            // Test URL extraction
            var extractedIds = ExtractFileAndNodeIds(figmaUrl);
            if (extractedIds != null)
            {
                Debug.Log($"✅ URL parsing successful - would extract:");
                Debug.Log($"  File ID: {extractedIds.Value.fileId}");
                Debug.Log($"  Node ID: {extractedIds.Value.nodeId}");
            }
            else
            {
                Debug.LogError("❌ URL parsing failed - check URL format");
            }
        }

        if (string.IsNullOrEmpty(fileId) || fileId == "YOUR_FILE_ID")
        {
            Debug.LogError("❌ File ID not set - use 'Extract IDs from URL' or set manually");
        }
        else
        {
            Debug.Log($"✅ File ID configured: {fileId}");
        }

        if (string.IsNullOrEmpty(nodeId) || nodeId == "YOUR_NODE_ID")
        {
            Debug.LogError("❌ Node ID not set - use 'Extract IDs from URL' or set manually");
        }
        else
        {
            Debug.Log($"✅ Node ID configured: {nodeId}");
        }

        Debug.Log(
            $"✅ Image download mode: {(downloadOnlyTargetNode ? "Target node only" : downloadChildrenImages ? "With children" : "Disabled")}"
        );
        Debug.Log("=== Validation Complete ===");
    }

    // IFigmaNodeConverter interface implementation
    public void ListAvailableNodes()
    {
        Debug.Log("Simple converter doesn't maintain a list of available nodes. Use the download and convert workflow.");
    }

    [ContextMenu("Generate Prefabs")]
    public void GeneratePrefab()
    {
        if (createdNodes.Count == 0)
        {
            Debug.LogWarning("No UI elements created yet. Please convert Figma data first.");
            return;
        }

        // Generate prefabs for elements with prefab prefix
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

        if (enableDebugLogs)
            Debug.Log("✓ Prefab generation completed!");
    }
}

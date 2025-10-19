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
/// Main Figma to Unity converter - Refactored with service-based architecture
/// Replaces FigmaSimpleConverter with better performance and maintainability
/// </summary>
public class FigmaConverter : MonoBehaviour
{
    [SerializeField]
    private FigmaConverterConfig config = new FigmaConverterConfig();

    [Header("Figma URL Input")]
    public string figmaUrl = "";

    // Services (created on demand)
    private SpriteCacheService _spriteCache;
    private NodeDataCacheService _nodeCache;
    private ObjectPoolService _objectPool;
    private UIElementFactory _uiFactory;
    private UITransformService _transformService;

    // Runtime state
    private Dictionary<string, GameObject> _createdNodes = new Dictionary<string, GameObject>();
    private JObject _currentNodeData;
    private bool _servicesInitialized = false;

    // Image fills cache for DirectSpriteGenerator
    private Dictionary<string, string> _imageFillsCache = new Dictionary<string, string>();

    #region Unity Lifecycle

    private void OnDestroy()
    {
        CleanupServices();
    }

    #endregion

    #region Service Initialization

    private void InitializeServices()
    {
        // Check if services are actually initialized (not just the flag)
        if (_servicesInitialized && _spriteCache != null && _nodeCache != null)
        {
            Debug.Log("✓ Services already initialized, skipping...");
            return;
        }

        // Force reinitialize if flag is set but services are null
        if (_servicesInitialized && (_spriteCache == null || _nodeCache == null))
        {
            Debug.LogWarning("Service flag was set but services were null. Reinitializing...");
            _servicesInitialized = false;
        }

        Debug.Log("Initializing services...");

        try
        {
            // Initialize caching services
            _spriteCache = new SpriteCacheService(config.spriteCacheSize);
            _nodeCache = new NodeDataCacheService(config.nodeCacheSize);

            // Initialize object pooling
            if (config.enableObjectPooling)
            {
                _objectPool = new ObjectPoolService();
            }

            // Initialize rendering services
            config.targetNodeId = config.nodeId; // Make available to factories
            _uiFactory = new UIElementFactory(config, _spriteCache, _nodeCache);

            _transformService = new UITransformService(config);

            _servicesInitialized = true;

            if (config.enableDebugLogs)
            {
                Debug.Log($"  Sprite Cache: {config.spriteCacheSize} MB");
                Debug.Log($"  Node Cache: {config.nodeCacheSize} entries");
                Debug.Log($"  Object Pooling: {config.enableObjectPooling}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to initialize services: {ex.Message}\n{ex.StackTrace}");
            _servicesInitialized = false;
        }
    }

    private void CleanupServices()
    {
        _spriteCache?.Clear();
        _nodeCache?.Clear();
        _objectPool?.ClearAll(false);
        _transformService?.ClearCache();
        _imageFillsCache?.Clear();
        _servicesInitialized = false;
    }

    #endregion

    #region Public API

    private void ExtractIdsFromUrl()
    {
        if (string.IsNullOrEmpty(figmaUrl))
        {
            Debug.LogError("Figma URL is empty! Please paste a Figma URL first.");
            return;
        }

        var extractedIds = FigmaUrlExtractor.ExtractFileAndNodeIds(figmaUrl);
        if (extractedIds != null)
        {
            config.fileId = extractedIds.Value.fileId;
            config.nodeId = extractedIds.Value.nodeId;

            if (config.enableDebugLogs)
            {
                Debug.Log($"✓ Extracted from URL:");
                Debug.Log($"  File ID: {config.fileId}");
                Debug.Log($"  Node ID: {config.nodeId}");
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        else
        {
            Debug.LogError("Failed to extract IDs from URL. Please check the URL format.");
        }
    }

    [ContextMenu("Download and Convert Everything")]
    public void DownloadAndConvertEverything()
    {
        ExtractIdsFromUrl();
        InitializeServices();
        StartCoroutine(DownloadAndConvertCoroutine());
    }

    [ContextMenu("Generate Prefabs")]
    public void GeneratePrefab()
    {
        if (_createdNodes.Count == 0)
        {
            Debug.LogWarning("No UI elements created yet. Please convert Figma data first.");
            return;
        }

#if UNITY_EDITOR
        foreach (var kvp in _createdNodes)
        {
            if (kvp.Value != null && kvp.Value.name.StartsWith(Constant.PREFAB_PREFIX))
            {
                string prefabName = kvp.Value.name.Replace(Constant.PREFAB_PREFIX, "");
                string localPath = $"Assets/Prefabs/{prefabName}.prefab";
                localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    kvp.Value,
                    localPath,
                    InteractionMode.UserAction
                );

                if (config.enableDebugLogs)
                    Debug.Log($"✓ Created prefab: {localPath}");
            }
        }

        if (config.enableDebugLogs)
            Debug.Log("✓ Prefab generation completed!");
#else
        Debug.LogWarning("Prefab generation only works in Unity Editor");
#endif
    }

    #endregion

    #region Download Operations

    private IEnumerator DownloadAndConvertCoroutine()
    {
        yield return DownloadNodeData();
        yield return DownloadImages();
        InitializeServices();
        StartCoroutine(ConvertNodeCoroutine());

        // Log performance stats
        if (config.enableDebugLogs)
        {
            Debug.Log($"✓ Cache Stats: {_spriteCache.GetStatistics()}");
            if (_objectPool != null)
                Debug.Log($"✓ Pool Stats: {_objectPool.GetStatistics()}");
        }
    }

    private IEnumerator DownloadNodeData()
    {
        if (config == null)
        {
            Debug.LogError("Config is null!");
            yield break;
        }

        if (_nodeCache == null)
        {
            Debug.LogError("NodeCache is null! Services may not be initialized.");
            yield break;
        }

        string encodedNodeId = UnityEngine.Networking.UnityWebRequest.EscapeURL(config.nodeId);
        string url = $"https://api.figma.com/v1/files/{config.fileId}/nodes?ids={encodedNodeId}";

        using (var www = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("X-FIGMA-TOKEN", config.figmaToken);
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string jsonContent = www.downloadHandler?.text;
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Debug.LogError("Downloaded content is empty!");
                    yield break;
                }

                JObject root = JObject.Parse(jsonContent);
                _currentNodeData = root["nodes"]?[config.nodeId]?["document"] as JObject;

                if (_currentNodeData != null)
                {
                    // Index the node tree for fast lookup
                    _nodeCache.IndexNodeTree(_currentNodeData);

                    if (config.enableDebugLogs)
                    {
                        Debug.Log($"✓ Downloaded: {_currentNodeData["name"]}");
                        Debug.Log($"✓ Indexed {_nodeCache.CachedNodeCount} nodes");
                    }

                    SaveNodeDataToResources(jsonContent);
                }
                else
                {
                    Debug.LogError($"Node not found: {config.nodeId}");
                }
            }
            else
            {
                Debug.LogError($"Download failed: {www.error}");
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
        yield return DownloadImageFills();
        List<string> imageNodeIds = new List<string>();
        CollectIconFrameIds(_currentNodeData, imageNodeIds);

        if (imageNodeIds.Count == 0)
        {
            if (config.enableDebugLogs)
                Debug.Log("No images or icons to download.");
            yield break;
        }

        if (config.enableDebugLogs)
            Debug.Log($"Found {imageNodeIds.Count} nodes to download as images");

        yield return DownloadImagesFromIds(imageNodeIds);
    }

    /// <summary>
    /// Downloads image fills using FigmaApi.GetImageFillsAsync and stores them for DirectSpriteGenerator
    /// </summary>
    private IEnumerator DownloadImageFills()
    {
        if (_currentNodeData == null)
        {
            Debug.LogError("No node data available for image fills download");
            yield break;
        }

        // Collect all imageRefs from the node tree
        List<string> imageRefs = CollectImageRefs(_currentNodeData);

        if (imageRefs.Count == 0)
        {
            if (config.enableDebugLogs)
                Debug.Log("No image fills found in node data.");
            yield break;
        }

        if (config.enableDebugLogs)
            Debug.Log(
                $"Found {imageRefs.Count} image fills to download: {string.Join(", ", imageRefs)}"
            );

        // Download image fills using FigmaApi
        var figmaApi = new FigmaApi(config.figmaToken);
        var imageFillsRequest = new ImageFillsRequest(config.fileId)
        {
            imageRefs = imageRefs.ToArray(),
        };

        Task<Dictionary<string, byte[]>> task = figmaApi.GetImageFillsAsync(
            imageFillsRequest,
            CancellationToken.None
        );

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogError(
                $"✗ Failed to download image fills: {task.Exception?.GetBaseException().Message}"
            );
            figmaApi.Dispose();
            yield break;
        }

        var figmaImageData = task.Result;
        if (figmaImageData == null || figmaImageData.Count == 0)
        {
            if (config.enableDebugLogs)
                Debug.LogWarning("No image fills were downloaded.");
            figmaApi.Dispose();
            yield break;
        }

        // Convert to DirectSpriteGenerator format and store in cache
        var imageData = SpriteGenerator.ConvertFigmaImageData(figmaImageData);
        StoreImageFillsInCache(imageData);

        if (config.enableDebugLogs)
        {
            Debug.Log($"✓ Downloaded {figmaImageData.Count} image fills");
            foreach (var kvp in figmaImageData)
            {
                if (kvp.Value != null)
                {
                    Debug.Log($"  - {kvp.Key}: {kvp.Value.Length} bytes");
                }
                else
                {
                    Debug.LogWarning($"  - {kvp.Key}: No data available");
                }
            }
        }

        figmaApi.Dispose();
    }

    private IEnumerator DownloadImagesFromIds(List<string> imageNodeIds)
    {
        var figmaApi = new FigmaApi(config.figmaToken);
        var imageRequest = new ImageRequest(config.fileId)
        {
            ids = imageNodeIds.ToArray(),
            format = config.imageFormat,
            scale = config.imageScale,
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
            figmaApi.Dispose();
            yield break;
        }

        var images = task.Result;
        if (images == null || images.Count == 0)
        {
            if (config.enableDebugLogs)
                Debug.LogWarning("No images were downloaded.");
            figmaApi.Dispose();
            yield break;
        }

        // Save downloaded images
        string resourcesSpritesPath = Path.Combine(
            Application.dataPath,
            Constant.RESOURCES_FOLDER,
            Constant.SAVE_IMAGE_FOLDER,
            config.nodeId.Replace(":", "-")
        );

        EnsureDirectory(resourcesSpritesPath);

        foreach (var kvp in images)
        {
            string imageNodeId = kvp.Key;
            byte[] imageData = kvp.Value;

            if (imageData == null)
            {
                if (config.enableDebugLogs)
                    Debug.LogWarning($"⚠ Image node {imageNodeId} returned null data");
                continue;
            }

            string nodeName = _nodeCache.GetNodeName(imageNodeId) ?? imageNodeId;
            string fileName = nodeName.SanitizeFileName();
            string filePath = Path.Combine(
                resourcesSpritesPath,
                $"{fileName}.{config.imageFormat}"
            );

            File.WriteAllBytes(filePath, imageData);

            if (config.enableDebugLogs)
                Debug.Log($"✓ Saved image: {fileName}.{config.imageFormat}");
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        figmaApi.Dispose();
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
                // && nodeName.StartsWith(Constant.IMAGE_PREFIX)
                && !imageNodeIds.Contains(nodeId)
            )
            {
                imageNodeIds.Add(nodeId);
            }

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

    private void CollectIconFrameIds(JToken token, List<string> iconFrameIds)
    {
        if (token == null)
            return;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();
            string nodeType = obj["type"]?.ToString();

            if (
                (nodeType == "FRAME" || nodeType == "GROUP" || nodeType == "COMPONENT")
                && !string.IsNullOrEmpty(nodeId)
                && FigmaIconDetector.IsIconFrame(obj)
            )
            {
                if (!iconFrameIds.Contains(nodeId))
                {
                    iconFrameIds.Add(nodeId);
                    if (config.enableDebugLogs)
                        Debug.Log($"Found icon frame: {obj["name"]} ({nodeId})");
                }
                return; // Don't recurse into children
            }

            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                CollectIconFrameIds(childrenToken, iconFrameIds);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
                CollectIconFrameIds(child, iconFrameIds);
        }
    }

    #endregion

    #region Conversion Operations

    private IEnumerator ConvertNodeCoroutine()
    {
        // Ensure we have a target canvas
        if (config.targetCanvas == null)
        {
            if (config.createNewCanvas)
            {
                CreateCanvas();
                if (config.enableDebugLogs)
                    Debug.Log("Created new canvas for UI conversion");
            }
            else
            {
                Debug.LogError("No target canvas found and createNewCanvas is disabled.");
                yield break;
            }
        }

        if (config.enableDebugLogs)
        {
            Debug.Log(
                $"Converting node: {_currentNodeData["name"]} (Type: {_currentNodeData["type"]})"
            );
        }

        try
        {
            ProcessFigmaNode(_currentNodeData, config.targetCanvas.transform);

            if (config.enableDebugLogs)
            {
                Debug.Log("✓ Figma to UI conversion completed!");
                Debug.Log($"✓ Created {_createdNodes.Count} UI elements");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during conversion: {ex.Message}");
            if (config.enableDebugLogs)
                Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private GameObject ProcessFigmaNode(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        if (config.enableDebugLogs)
            Debug.Log($"Processing: {nodeName} (ID: {nodeId}, Type: {nodeType})");

        // Check if node has image fills that need DirectSpriteGenerator
        if (HasImageFills(nodeData))
        {
            if (config.enableDebugLogs)
                Debug.Log($"Node {nodeName} has image fills, using DirectSpriteGenerator");

            // Use DirectSpriteGenerator for nodes with image fills
            return ProcessNodeWithImageFills(nodeData, parent);
        }

        // Create UI element using factory for regular nodes
        GameObject nodeGameObject = _uiFactory.CreateUIElement(nodeData, parent);

        if (nodeGameObject != null)
        {
            // Cache created node
            if (!string.IsNullOrEmpty(nodeId))
            {
                _createdNodes[nodeId] = nodeGameObject;
            }

            // Apply transform and visibility
            _transformService.ApplyTransform(nodeData, nodeGameObject, config.targetCanvas);
            _transformService.ApplyVisibility(nodeData, nodeGameObject);

            // Process children (if not an icon frame)
            bool isIconFrame =
                (nodeType == "FRAME" || nodeType == "GROUP" || nodeType == "COMPONENT")
                && FigmaIconDetector.IsIconFrame(nodeData);

            if (!isIconFrame && nodeData["children"] is JArray children)
            {
                foreach (JObject child in children)
                {
                    ProcessFigmaNode(child, nodeGameObject.transform);
                }
            }
        }

        return nodeGameObject;
    }

    /// <summary>
    /// Processes nodes with image fills using DirectSpriteGenerator
    /// </summary>
    private GameObject ProcessNodeWithImageFills(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        if (config.enableDebugLogs)
            Debug.Log($"Processing node with image fills: {nodeName} (Type: {nodeType})");

        // Get node dimensions
        float width = nodeData["size"]?["x"]?.ToObject<float>() ?? 100f;
        float height = nodeData["size"]?["y"]?.ToObject<float>() ?? 100f;

        if (config.enableDebugLogs)
            Debug.Log($"Node dimensions: {width}x{height}");

        // Get image fills data from cache
        var imageData = GetImageFillsFromCache();

        if (config.enableDebugLogs)
        {
            Debug.Log($"Image fills cache contains {imageData.Count} entries");
            foreach (var kvp in imageData)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value?.Length ?? 0} characters");
            }
        }

        // Create GameObject for this node
        GameObject nodeGameObject = new GameObject(nodeName);
        nodeGameObject.transform.SetParent(parent, false);

        // Add Image component
        UnityEngine.UI.Image imageComponent = nodeGameObject.AddComponent<UnityEngine.UI.Image>();

        if (imageComponent == null)
        {
            Debug.LogError($"Failed to add Image component to {nodeName}");
            return nodeGameObject;
        }

        // Generate sprite using DirectSpriteGenerator
        StartCoroutine(GenerateSpriteForNode(nodeData, width, height, imageData, imageComponent));

        // Cache created node
        if (!string.IsNullOrEmpty(nodeId))
        {
            _createdNodes[nodeId] = nodeGameObject;
        }

        // Apply transform and visibility
        _transformService.ApplyTransform(nodeData, nodeGameObject, config.targetCanvas);
        _transformService.ApplyVisibility(nodeData, nodeGameObject);

        // Process children
        if (nodeData["children"] is JArray children)
        {
            foreach (JObject child in children)
            {
                ProcessFigmaNode(child, nodeGameObject.transform);
            }
        }

        return nodeGameObject;
    }

    /// <summary>
    /// Generates sprite for node with image fills using DirectSpriteGenerator
    /// </summary>
    private IEnumerator GenerateSpriteForNode(
        JObject nodeData,
        float width,
        float height,
        Dictionary<string, string> imageData,
        UnityEngine.UI.Image imageComponent
    )
    {
        string nodeName = nodeData["name"]?.ToString() ?? "Unknown";

        if (config.enableDebugLogs)
        {
            Debug.Log($"Generating sprite for {nodeName} (Size: {width}x{height})");
            Debug.Log($"Image data available: {imageData?.Count ?? 0} entries");
        }

        // Use DirectSpriteGenerator to generate sprite from Resources or create new one
        yield return SpriteGenerator.GenerateSpriteFromResourcesOrCreateAsync(
            nodeData,
            width,
            height,
            imageData,
            (sprite) =>
            {
                if (sprite != null && imageComponent != null)
                {
                    imageComponent.sprite = sprite;
                    if (config.enableDebugLogs)
                        Debug.Log(
                            $"✓ Generated/loaded sprite for {nodeName}: {sprite.name} ({sprite.rect.width}x{sprite.rect.height})"
                        );
                }
                else
                {
                    if (sprite == null)
                    {
                        Debug.LogError(
                            $"Failed to generate/load sprite for {nodeName} - sprite is null"
                        );
                    }
                    if (imageComponent == null)
                    {
                        Debug.LogError(
                            $"Failed to assign sprite for {nodeName} - imageComponent is null"
                        );
                    }

                    // Log additional debug info
                    Debug.LogError($"Node data: {nodeData}");
                    Debug.LogError($"Image data count: {imageData?.Count ?? 0}");
                    if (imageData != null)
                    {
                        foreach (var kvp in imageData)
                        {
                            Debug.LogError(
                                $"  ImageRef: {kvp.Key}, Data length: {kvp.Value?.Length ?? 0}"
                            );
                        }
                    }
                }
            },
            config.nodeId // Pass main nodeId for Resources lookup
        );
    }

    /// <summary>
    /// Checks if node has image fills
    /// </summary>
    private bool HasImageFills(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null)
        {
            foreach (JObject fill in fills)
            {
                string fillType = fill["type"]?.ToString();
                if (fillType == "IMAGE")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void CreateCanvas()
    {
        GameObject canvasGO = new GameObject(config.canvasName);
        config.targetCanvas = canvasGO.AddComponent<Canvas>();
        config.targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (config.enableDebugLogs)
            Debug.Log($"✓ Created Canvas: {config.canvasName}");
    }

    #endregion

    #region Image Fills Support

    /// <summary>
    /// Collects all imageRefs from node data recursively
    /// </summary>
    private List<string> CollectImageRefs(JToken token)
    {
        var imageRefs = new List<string>();

        if (token == null)
            return imageRefs;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;

            // Check fills array for image fills
            JArray fills = obj["fills"] as JArray;
            if (fills != null)
            {
                foreach (JObject fill in fills)
                {
                    string fillType = fill["type"]?.ToString();
                    if (fillType == "IMAGE")
                    {
                        string imageRef = fill["imageRef"]?.ToString();
                        if (!string.IsNullOrEmpty(imageRef) && !imageRefs.Contains(imageRef))
                        {
                            imageRefs.Add(imageRef);
                        }
                    }
                }
            }

            // Check children recursively
            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                imageRefs.AddRange(CollectImageRefs(childrenToken));
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
                imageRefs.AddRange(CollectImageRefs(child));
        }

        return imageRefs;
    }

    /// <summary>
    /// Stores image fills data in cache for DirectSpriteGenerator to use
    /// </summary>
    private void StoreImageFillsInCache(Dictionary<string, string> imageData)
    {
        if (imageData == null || imageData.Count == 0)
            return;

        // Store in local cache
        foreach (var kvp in imageData)
        {
            _imageFillsCache[kvp.Key] = kvp.Value;
        }

        if (config.enableDebugLogs)
            Debug.Log($"✓ Stored {imageData.Count} image fills in cache");
    }

    /// <summary>
    /// Gets image fills data from cache
    /// </summary>
    public Dictionary<string, string> GetImageFillsFromCache()
    {
        return new Dictionary<string, string>(_imageFillsCache);
    }

    #endregion

    #region Helper Methods

    private void SaveNodeDataToResources(string jsonContent)
    {
        try
        {
            string sanitizedNodeId = config.nodeId.Replace(":", "-");
            string folderPath = Path.Combine(Application.dataPath, "Resources", "FigmaData");
            EnsureDirectory(folderPath);

            string fileName = $"figma_node_{sanitizedNodeId}.json";
            string filePath = Path.Combine(folderPath, fileName);

            File.WriteAllText(filePath, jsonContent);

            if (config.enableDebugLogs)
                Debug.Log($"✓ Saved: {fileName}");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Save failed: {ex.Message}");
        }
    }

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    #endregion
}

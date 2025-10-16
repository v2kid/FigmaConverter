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
public class FigmaConverter : MonoBehaviour, IFigmaNodeConverter
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
        _servicesInitialized = false;
    }

    #endregion

    #region Public API

    [ContextMenu("Extract IDs from URL")]
    public void ExtractIdsFromUrl()
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

    [ContextMenu("Convert to UI")]
    public void ConvertNodeToUI()
    {
        if (_currentNodeData == null)
        {
            Debug.LogError("No node data available. Please download data first.");
            return;
        }

        if (string.IsNullOrEmpty(config.nodeId))
        {
            Debug.LogError("Node ID is not set!");
            return;
        }

        InitializeServices();
        StartCoroutine(ConvertNodeCoroutine());
    }

    [ContextMenu("Clear Created UI")]
    public void ClearCreatedUI()
    {
        foreach (var kvp in _createdNodes)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
            }
        }

        _createdNodes.Clear();
        CleanupServices();

        if (config.enableDebugLogs)
            Debug.Log("✓ Cleared UI and services");
    }

    public void ValidateSetup()
    {
        bool valid = true;

        if (string.IsNullOrEmpty(config.figmaToken) || config.figmaToken == "YOUR_FIGMA_TOKEN")
        {
            Debug.LogError("Figma token not set");
            valid = false;
        }

        if (string.IsNullOrEmpty(config.fileId) || config.fileId == "YOUR_FILE_ID")
        {
            Debug.LogError("File ID not set");
            valid = false;
        }

        if (string.IsNullOrEmpty(config.nodeId) || config.nodeId == "YOUR_NODE_ID")
        {
            Debug.LogError("Node ID not set");
            valid = false;
        }

        if (valid && config.enableDebugLogs)
        {
            Debug.Log("✓ Setup valid");
        }
    }

    public void ListAvailableNodes()
    {
        Debug.Log("Use the download and convert workflow.");
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

    [ContextMenu("Show Sprite Info")]
    public void ShowSpriteInfo()
    {
        if (string.IsNullOrEmpty(config.nodeId))
        {
            Debug.LogWarning("Node ID not set. Cannot check sprite location.");
            return;
        }

        string sanitizedNodeId = config.nodeId.Replace(":", "-");
        string spriteFolderPath = System.IO.Path.Combine(
            Application.dataPath,
            "Resources",
            "Sprites",
            sanitizedNodeId
        );

        if (System.IO.Directory.Exists(spriteFolderPath))
        {
            string[] files = System.IO.Directory.GetFiles(spriteFolderPath, "*.png");
            Debug.Log($"✓ Sprite folder: Assets/Resources/Sprites/{sanitizedNodeId}");
            Debug.Log($"✓ Total sprites saved: {files.Length}");

            if (config.enableDebugLogs && files.Length > 0)
            {
                Debug.Log("Saved sprites:");
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    Debug.Log($"  - {fileName}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"No sprites saved yet for node {sanitizedNodeId}");
            Debug.Log("Sprites will be saved automatically when you run 'Download and Convert Everything'");
        }
    }

    #endregion

    #region Download Operations

    private IEnumerator DownloadAndConvertCoroutine()
    {
        yield return DownloadNodeData();
        yield return DownloadImages();
        ConvertNodeToUI();

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

        List<string> imageNodeIds = new List<string>();

        // Collect image nodes and icon frames
        CollectImageNodeIds(_currentNodeData, imageNodeIds);
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
            "Resources",
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
                && nodeName.StartsWith(Constant.IMAGE_PREFIX)
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

        // Create UI element using factory
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

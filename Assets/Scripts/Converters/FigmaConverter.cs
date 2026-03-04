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
    private GoogleFontService _fontService;
    private ShapeBaker _shapeBaker;

    // Runtime state
    private Dictionary<string, GameObject> _createdNodes = new Dictionary<string, GameObject>();
    private JObject _currentNodeData;
    private bool _servicesInitialized = false;

    // Image fills cache (imageRef -> base64 data)
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

            // Initialize font service
            _fontService = new GoogleFontService(config);

            // Initialize shape baking service
            _shapeBaker = new ShapeBaker(config);

            // Initialize rendering services
            config.targetNodeId = config.nodeId;
            _uiFactory = new UIElementFactory(config, _spriteCache, _nodeCache, _fontService, _shapeBaker);

            _transformService = new UITransformService(config);

            _servicesInitialized = true;
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
        _shapeBaker?.Dispose();
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
            }
        }

#else
        Debug.LogWarning("Prefab generation only works in Unity Editor");
#endif
    }

    #endregion

    #region Download Operations

    private IEnumerator DownloadAndConvertCoroutine()
    {
        yield return DownloadNodeData();
        yield return ResolveFonts();
        yield return DownloadImages();
        InitializeServices();
        StartCoroutine(ConvertNodeCoroutine());
    }

    private IEnumerator ResolveFonts()
    {
        if (_currentNodeData == null || _fontService == null)
        {
            Debug.LogWarning("Cannot resolve fonts: no node data or font service");
            yield break;
        }

        var families = _fontService.CollectUsedFontFamilies(_currentNodeData);
        if (families.Count > 0)
        {
            yield return _fontService.ResolveFontsCoroutine(families);
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
            yield break;
        }

        yield return DownloadImagesFromIds(imageNodeIds);
    }

    /// <summary>
    /// Downloads image fills using FigmaApi.GetImageFillsAsync and stores them in cache
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
            yield break;
        }

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
            figmaApi.Dispose();
            yield break;
        }

        // Convert byte[] to base64 strings and store in cache
        var imageData = new Dictionary<string, string>();
        foreach (var kvp in figmaImageData)
        {
            if (kvp.Value != null)
            {
                imageData[kvp.Key] = ImageRenderer.ConvertImageDataToBase64(kvp.Value);
            }
        }
        StoreImageFillsInCache(imageData);
        figmaApi.Dispose();
    }

    private IEnumerator DownloadImagesFromIds(List<string> imageNodeIds)
    {
        // Pre-download check: filter out IDs whose images already exist on disk
        string resourcesSpritesPath = Path.Combine(
            Application.dataPath,
            Constant.RESOURCES_FOLDER,
            Constant.SAVE_IMAGE_FOLDER,
            config.nodeId.Replace(":", "-")
        );
        EnsureDirectory(resourcesSpritesPath);

        List<string> idsToDownload = new List<string>();
        int alreadyExistCount = 0;

        foreach (string nodeId in imageNodeIds)
        {
            string nodeName = _nodeCache.GetNodeName(nodeId) ?? nodeId;
            string fileName = nodeName.SanitizeFileName();
            string filePath = Path.Combine(resourcesSpritesPath, $"{fileName}.{config.imageFormat}");

            if (File.Exists(filePath))
            {
                alreadyExistCount++;
                Debug.Log($"⏭ Image already exists, skipping download: {fileName}");
            }
            else
            {
                idsToDownload.Add(nodeId);
            }
        }

        if (alreadyExistCount > 0)
        {
            Debug.Log($"✓ Skipped {alreadyExistCount} images (already on disk). Downloading {idsToDownload.Count} remaining.");
        }

        if (idsToDownload.Count == 0)
        {
            Debug.Log("✓ All images already exist. No API calls needed!");
            yield break;
        }

        var figmaApi = new FigmaApi(config.figmaToken);
        var imageRequest = new ImageRequest(config.fileId)
        {
            ids = idsToDownload.ToArray(),
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
            figmaApi.Dispose();
            yield break;
        }

        // Save downloaded images
        // Track saved file names to avoid writing duplicate images
        HashSet<string> savedFileNames = new HashSet<string>();
        int skippedCount = 0;

        foreach (var kvp in images)
        {
            string imageNodeId = kvp.Key;
            byte[] imageData = kvp.Value;

            if (imageData == null)
            {
                continue;
            }

            string nodeName = _nodeCache.GetNodeName(imageNodeId) ?? imageNodeId;
            string fileName = nodeName.SanitizeFileName();

            // Deduplication: skip if a file with the same name was already saved
            if (savedFileNames.Contains(fileName))
            {
                skippedCount++;
                Debug.Log($"⏭ Skipping duplicate image (same name): {fileName} (node ID: {imageNodeId})");
                continue;
            }

            string filePath = Path.Combine(
                resourcesSpritesPath,
                $"{fileName}.{config.imageFormat}"
            );
            File.WriteAllBytes(filePath, imageData);
            savedFileNames.Add(fileName);
        }

        if (skippedCount > 0)
        {
            Debug.Log($"✓ Image download complete. Skipped {skippedCount} duplicate images by name.");
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        figmaApi.Dispose();
    }



    private void CollectIconFrameIds(JToken token, List<string> iconFrameIds, HashSet<string> collectedNames = null)
    {
        if (token == null)
            return;

        // Initialize name tracking set on first call
        if (collectedNames == null)
            collectedNames = new HashSet<string>();

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();
            string nodeType = obj["type"]?.ToString();

            // Check visibility if skipInvisibleItems is enabled
            if (config.skipInvisibleItems)
            {
                bool visible = obj["visible"]?.ToObject<bool>() ?? true;
                if (!visible)
                {
                    // Skip this node but still process children
                    if (obj.TryGetValue("children", out JToken childToken))
                    {
                        CollectIconFrameIds(childToken, iconFrameIds, collectedNames);
                    }
                    return;
                }
            }

            if (
                (nodeType == "FRAME" || nodeType == "GROUP" || nodeType == "COMPONENT")
                && !string.IsNullOrEmpty(nodeId)
                && FigmaIconDetector.IsIconFrame(obj)
            )
            {
                string nodeName = obj["name"]?.ToString() ?? "";
                string sanitizedName = nodeName.SanitizeFileName();

                // Dedup by name: skip if we already collected an icon with the same name
                if (collectedNames.Contains(sanitizedName))
                {
                    Debug.Log($"⏭ Skipping duplicate icon frame (same name): {nodeName} (node ID: {nodeId})");
                    return;
                }

                if (!iconFrameIds.Contains(nodeId))
                {
                    iconFrameIds.Add(nodeId);
                    collectedNames.Add(sanitizedName);
                }
                return; // Don't recurse into children
            }

            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                CollectIconFrameIds(childrenToken, iconFrameIds, collectedNames);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
                CollectIconFrameIds(child, iconFrameIds, collectedNames);
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
            }
            else
            {
                Debug.LogError("No target canvas found and createNewCanvas is disabled.");
                yield break;
            }
        }

        try
        {
            ProcessFigmaNode(_currentNodeData, config.targetCanvas.transform);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during conversion: {ex.Message}");
        }
    }

    private GameObject ProcessFigmaNode(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        // Check visibility and skip processing if skipInvisibleItems is enabled
        if (config.skipInvisibleItems)
        {
            bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
            {
                Debug.Log($"Skipping invisible node: {nodeName} (ID: {nodeId})");
                return null;
            }
        }

        if (HasImageFills(nodeData))
        {
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
    /// Processes nodes with image fills
    /// </summary>
    private GameObject ProcessNodeWithImageFills(JObject nodeData, Transform parent)
    {
        string nodeId = nodeData["id"]?.ToString();
        string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
        string nodeType = nodeData["type"]?.ToString();

        // Check visibility and skip processing if skipInvisibleItems is enabled
        if (config.skipInvisibleItems)
        {
            bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
            {
                Debug.Log($"Skipping invisible node with image fills: {nodeName} (ID: {nodeId})");
                return null;
            }
        }

        // Get node dimensions
        float width = nodeData["size"]?["x"]?.ToObject<float>() ?? 100f;
        float height = nodeData["size"]?["y"]?.ToObject<float>() ?? 100f;
        var imageData = GetImageFillsFromCache();
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

        StartCoroutine(GenerateSpriteForNode(nodeData, width, height, imageData, imageComponent));

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
    /// Generates sprite for node with image fills using ImageRenderer
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
        string nodeId = nodeData["id"]?.ToString();

        string imageRef = GetImageRefFromNode(nodeData);
        if (string.IsNullOrEmpty(imageRef))
        {
            Debug.LogError($"No imageRef found in fills for node {nodeName} (ID: {nodeId})");
            yield break;
        }

        // Check if we have image data for this imageRef
        if (imageData == null || !imageData.ContainsKey(imageRef))
        {
            Debug.LogError(
                $"No image data found for imageRef {imageRef} in node {nodeName} (ID: {nodeId})"
            );
            yield break;
        }

        string base64ImageData = imageData[imageRef];
        if (string.IsNullOrEmpty(base64ImageData))
        {
            Debug.LogError(
                $"Empty image data for imageRef {imageRef} in node {nodeName} (ID: {nodeId})"
            );
            yield break;
        }

        // Use ImageRenderer to load image from base64 data
        Texture2D imageTexture = ImageRenderer.LoadImageFromBase64(base64ImageData);
        if (imageTexture == null)
        {
            Debug.LogError($"Failed to load image texture for {nodeName} from base64 data");
            yield break;
        }

        // Create sprite from texture
        Sprite sprite = Sprite.Create(
            imageTexture,
            new Rect(0, 0, imageTexture.width, imageTexture.height),
            new Vector2(0.5f, 0.5f)
        );

        // if (sprite != null && imageComponent != null)
        // {
        //     imageComponent.sprite = sprite;
        //     // Save sprite to Resources for future use
        //     if (!string.IsNullOrEmpty(config.nodeId))
        //     {
        //         SpriteSaver.SaveSpriteToResources(sprite, nodeName, config.nodeId);
        //     }
        // }
        // else
        // {
        //     if (sprite == null)
        //     {
        //         Debug.LogError($"Failed to create sprite for {nodeName} - sprite is null");
        //     }
        //     if (imageComponent == null)
        //     {
        //         Debug.LogError($"Failed to assign sprite for {nodeName} - imageComponent is null");
        //     }
        // }
        // ... (Code tạo Sprite và SaveSpriteToResources)

        if (sprite != null && imageComponent != null)
        {
            // 1. Gán Sprite TẠM THỜI (được tạo tại runtime) vào Image component
            imageComponent.sprite = sprite;

            // 2. Lưu Sprite vào Resources và nhận về đường dẫn asset
            if (!string.IsNullOrEmpty(config.nodeId))
            {
                string resourcePath = SpriteSaver.SaveSpriteToResources(sprite, nodeName, config.nodeId);

                // --- Bổ sung sau khi save thành công ---
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    // Tải lại Sprite asset đã được lưu từ Resources
                    // Lưu ý: Resources.Load chỉ hoạt động ở Editor HOẶC Runtime sau khi đã BUILD
                    Sprite savedSprite = Resources.Load<Sprite>(resourcePath);

                    if (savedSprite != null)
                    {
                        // 3. Cập nhật Image component để trỏ đến Sprite asset ĐÃ LƯU
                        imageComponent.sprite = savedSprite;

                        // Tùy chọn: Xóa Sprite tạm thời đã tạo lúc đầu (vì Image component đã trỏ sang cái mới)
                        UnityEngine.Object.Destroy(sprite);

                        Debug.Log($"Successfully replaced temporary sprite with Resources asset: {resourcePath}");
                    }
                    else
                    {
                        Debug.LogError($"Failed to load saved sprite from Resources path: {resourcePath}");
                    }
                }
            }
        }

        yield return null;
    }

    /// <summary>
    /// Gets the imageRef from node's fills
    /// </summary>
    private string GetImageRefFromNode(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills != null)
        {
            foreach (JObject fill in fills)
            {
                string fillType = fill["type"]?.ToString();
                if (fillType == "IMAGE")
                {
                    return fill["imageRef"]?.ToString();
                }
            }
        }
        return null;
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

            // Check visibility if skipInvisibleItems is enabled
            if (config.skipInvisibleItems)
            {
                bool visible = obj["visible"]?.ToObject<bool>() ?? true;
                if (!visible)
                {
                    // Skip this node but still process children
                    if (obj.TryGetValue("children", out JToken childToken))
                    {
                        imageRefs.AddRange(CollectImageRefs(childToken));
                    }
                    return imageRefs;
                }
            }

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
    /// Stores image fills data in cache
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
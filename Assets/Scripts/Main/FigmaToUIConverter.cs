// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using Newtonsoft.Json.Linq;

// #if UNITY_EDITOR
// using UnityEditor;
// #endif

// public class FigmaToUIConverter : MonoBehaviour
// {
//     [Header("Node Settings")]
//     public string node_convert = "119:441";

//     [Header("UI Settings")]
//     public Canvas targetCanvas;
//     public bool createNewCanvas = true;
//     public string canvasName = "FigmaUI_Canvas";
//     public TMP_FontAsset defaultFont;
//     public Color defaultTextColor = Color.black;
//     public float scaleFactor = 1f;

//     [Header("Debug")]
//     public bool enableDebugLogs = true;

//     private Dictionary<string, GameObject> createdNodes = new Dictionary<string, GameObject>();

//     void Start()
//     {
//         // Setup canvas if needed
//         if (createNewCanvas && targetCanvas == null)
//         {
//             CreateCanvas();
//         }
//     }

//     public void CreateCanvas()
//     {
//         GameObject canvasGO = new GameObject(canvasName);
//         targetCanvas = canvasGO.AddComponent<Canvas>();
//         targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

//         // Add Canvas Scaler
//         CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
//         scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
//         scaler.referenceResolution = new Vector2(1920, 1080);
//         scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
//         scaler.matchWidthOrHeight = 0.5f;

//         // Add GraphicRaycaster
//         canvasGO.AddComponent<GraphicRaycaster>();

//         if (enableDebugLogs)
//             Debug.Log($"✓ Created Canvas: {canvasName}");
//     }

//     private IEnumerator ConvertFigmaToUI()
//     {
//         // Validate node_convert field
//         if (string.IsNullOrEmpty(node_convert))
//         {
//             Debug.LogError("node_convert is null or empty. Please set the node ID to convert.");
//             yield break;
//         }

//         // Get the cached file path from Resources
//         string cachedFilePath = Path.Combine(Application.dataPath, "Resources", "FigmaData",
//             $"figma_node_{node_convert.Replace(":", "-")}.json");

//         if (!File.Exists(cachedFilePath))
//         {
//             Debug.LogError($"Figma data not found at: {cachedFilePath}");
//             Debug.LogError("Please download Figma data first using FigmaDownloader.");
//             yield break;
//         }

//         // Ensure we have a target canvas
//         if (targetCanvas == null)
//         {
//             if (createNewCanvas)
//             {
//                 CreateCanvas();
//                 if (enableDebugLogs)
//                     Debug.Log("Created new canvas for UI conversion");
//             }
//             else
//             {
//                 Debug.LogError("No target canvas found and createNewCanvas is disabled.");
//                 yield break;
//             }
//         }

//         // Read and parse JSON
//         string jsonData = File.ReadAllText(cachedFilePath);
//         JObject root = JObject.Parse(jsonData);

//         if (enableDebugLogs)
//         {
//             Debug.Log($"JSON structure keys: {string.Join(", ", root.Properties().Select(p => p.Name))}");
//         }

//         if (root["nodes"] != null)
//         {
//             foreach (var nodeProperty in root["nodes"])
//             {
//                 if (nodeProperty is JProperty prop)
//                 {
//                     JObject nodeData = prop.Value as JObject;
//                     if (nodeData != null)
//                     {
//                         try
//                         {
//                             ProcessFigmaNode(nodeData, targetCanvas.transform);
//                         }
//                         catch (System.Exception ex)
//                         {
//                             Debug.LogError($"Error processing node: {prop.Name}. Error: {ex.Message}");
//                             if (enableDebugLogs)
//                                 Debug.LogError($"Node data: {nodeData}");
//                         }
//                     }
//                 }
//             }
//         }
//         else if (root["document"] != null)
//         {
//             // Alternative structure: document > children
//             if (enableDebugLogs)
//                 Debug.Log("Using document structure");
//             try
//             {
//                 ProcessFigmaNode(root["document"] as JObject, targetCanvas.transform);
//             }
//             catch (System.Exception ex)
//             {
//                 Debug.LogError($"Error processing document node: {ex.Message}");
//             }
//         }
//         else
//         {
//             // Try to process the root as a single node
//             if (enableDebugLogs)
//                 Debug.Log("Attempting to process root as single node");
//             try
//             {
//                 ProcessFigmaNode(root, targetCanvas.transform);
//             }
//             catch (System.Exception ex)
//             {
//                 Debug.LogError($"Error processing root node: {ex.Message}");
//             }
//         }

//         // Conversion completed

//         if (enableDebugLogs)
//             Debug.Log("✓ Figma to UI conversion completed!");
//     }

//     private GameObject ProcessFigmaNode(JObject nodeData, Transform parent)
//     {
//         string nodeId = nodeData["id"]?.ToString();
//         string nodeName = nodeData["name"]?.ToString() ?? "UnnamedNode";
//         string nodeType = nodeData["type"]?.ToString();

//         // Generate fallback ID if null
//         if (string.IsNullOrEmpty(nodeId))
//         {
//             nodeId = System.Guid.NewGuid().ToString();
//             if (enableDebugLogs)
//                 Debug.LogWarning($"Node ID is null, generated fallback: {nodeId}");
//         }

//         if (enableDebugLogs)
//             Debug.Log($"Processing node: {nodeName} (ID: {nodeId}, Type: {nodeType})");

//         GameObject nodeGameObject = null;

//         switch (nodeType?.ToUpper())
//         {
//             case "FRAME":
//             case "GROUP":
//             case "COMPONENT":
//             case "INSTANCE":
//                 nodeGameObject = CreateContainerNode(nodeData, parent);
//                 break;

//             case "TEXT":
//                 nodeGameObject = CreateTextNode(nodeData, parent);
//                 break;

//             case "RECTANGLE":
//             case "ELLIPSE":
//             case "ROUNDED_RECTANGLE":
//                 nodeGameObject = CreateImageNode(nodeData, parent);
//                 break;

//             case "VECTOR":
//             case "STAR":
//             case "POLYGON":
//             case "BOOLEAN_OPERATION":
//                 nodeGameObject = CreateVectorNode(nodeData, parent);
//                 break;

//             default:
//                 nodeGameObject = CreateGenericNode(nodeData, parent);
//                 break;
//         }

//         if (nodeGameObject != null)
//         {
//             // Only add to dictionary if nodeId is not null or empty
//             if (!string.IsNullOrEmpty(nodeId))
//             {
//                 createdNodes[nodeId] = nodeGameObject;
//             }

//             // Apply common properties
//             ApplyTransform(nodeData, nodeGameObject);
//             ApplyVisibility(nodeData, nodeGameObject);

//             // Process children if they exist
//             if (nodeData["children"] is JArray children)
//             {
//                 foreach (JObject child in children)
//                 {
//                     ProcessFigmaNode(child, nodeGameObject.transform);
//                 }
//             }
//         }

//         return nodeGameObject;
//     }

//     private GameObject CreateContainerNode(JObject nodeData, Transform parent)
//     {
//         string nodeName = nodeData["name"]?.ToString() ?? "Container";

//         GameObject container = new GameObject(nodeName);
//         container.transform.SetParent(parent);

//         // Add RectTransform
//         RectTransform rectTransform = container.AddComponent<RectTransform>();

//         // Add Image component for background if fills exist
//         JArray fills = nodeData["fills"] as JArray;
//         if (fills != null && fills.Count > 0)
//         {
//             Image backgroundImage = container.AddComponent<Image>();
//             ApplyFills(fills, backgroundImage);
//         }

//         return container;
//     }

//     private GameObject CreateTextNode(JObject nodeData, Transform parent)
//     {
//         string nodeName = nodeData["name"]?.ToString() ?? "Text";

//         GameObject textGO = new GameObject(nodeName);
//         textGO.transform.SetParent(parent);

//         // Add RectTransform
//         RectTransform rectTransform = textGO.AddComponent<RectTransform>();

//         // Add TextMeshProUGUI component
//         TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();

//         // Set text content
//         string characters = nodeData["characters"]?.ToString() ?? "Sample Text";
//         tmpText.text = characters;

//         // Apply font
//         if (defaultFont != null)
//             tmpText.font = defaultFont;

//         // Apply text styling
//         ApplyTextStyling(nodeData, tmpText);

//         // Apply fills for text color
//         JArray fills = nodeData["fills"] as JArray;
//         if (fills != null && fills.Count > 0)
//         {
//             ApplyTextFills(fills, tmpText);
//         }
//         else
//         {
//             tmpText.color = defaultTextColor;
//         }

//         return textGO;
//     }

//     private GameObject CreateImageNode(JObject nodeData, Transform parent)
//     {
//         string nodeName = nodeData["name"]?.ToString() ?? "Image";

//         GameObject imageGO = new GameObject(nodeName);
//         imageGO.transform.SetParent(parent);

//         // Add RectTransform
//         RectTransform rectTransform = imageGO.AddComponent<RectTransform>();

//         // Add Image component
//         Image image = imageGO.AddComponent<Image>();

//         // Apply fills
//         JArray fills = nodeData["fills"] as JArray;
//         if (fills != null && fills.Count > 0)
//         {
//             ApplyFills(fills, image);
//         }

//         // Apply corner radius for rounded rectangles
//         string nodeType = nodeData["type"]?.ToString();
//         if (nodeType == "RECTANGLE" || nodeType == "ROUNDED_RECTANGLE")
//         {
//             ApplyCornerRadius(nodeData, imageGO);
//         }

//         return imageGO;
//     }

//     private GameObject CreateVectorNode(JObject nodeData, Transform parent)
//     {
//         string nodeName = nodeData["name"]?.ToString() ?? "Vector";

//         GameObject vectorGO = new GameObject(nodeName);
//         vectorGO.transform.SetParent(parent);

//         // Add RectTransform
//         RectTransform rectTransform = vectorGO.AddComponent<RectTransform>();

//         // Add Image component (vectors will be treated as images for now)
//         Image image = vectorGO.AddComponent<Image>();

//         // Apply fills
//         JArray fills = nodeData["fills"] as JArray;
//         if (fills != null && fills.Count > 0)
//         {
//             ApplyFills(fills, image);
//         }

//         return vectorGO;
//     }

//     private GameObject CreateGenericNode(JObject nodeData, Transform parent)
//     {
//         string nodeName = nodeData["name"]?.ToString() ?? "GenericNode";

//         GameObject genericGO = new GameObject(nodeName);
//         genericGO.transform.SetParent(parent);

//         // Add RectTransform
//         RectTransform rectTransform = genericGO.AddComponent<RectTransform>();

//         return genericGO;
//     }

//     private void ApplyTransform(JObject nodeData, GameObject gameObject)
//     {
//         RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
//         if (rectTransform == null) return;

//         // Get bounding box
//         JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
//         if (boundingBox != null)
//         {
//             float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
//             float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
//             float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
//             float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

//             // Apply scale factor
//             x *= scaleFactor;
//             y *= scaleFactor;
//             width *= scaleFactor;
//             height *= scaleFactor;

//             // Set size
//             rectTransform.sizeDelta = new Vector2(width, height);

//             // Set position (Figma Y is inverted compared to Unity)
//             rectTransform.anchoredPosition = new Vector2(x, -y);

//             // Set anchors to top-left
//             rectTransform.anchorMin = Vector2.zero;
//             rectTransform.anchorMax = Vector2.zero;
//             rectTransform.pivot = Vector2.zero;
//         }
//     }

//     private void ApplyVisibility(JObject nodeData, GameObject gameObject)
//     {
//         bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
//         gameObject.SetActive(visible);
//     }

//     private void ApplyFills(JArray fills, Image image)
//     {
//         if (fills == null || fills.Count == 0) return;

//         JObject firstFill = fills[0] as JObject;
//         if (firstFill == null) return;

//         string fillType = firstFill["type"]?.ToString();

//         if (fillType == "SOLID")
//         {
//             JObject colorObj = firstFill["color"] as JObject;
//             if (colorObj != null)
//             {
//                 float r = colorObj["r"]?.ToObject<float>() ?? 1f;
//                 float g = colorObj["g"]?.ToObject<float>() ?? 1f;
//                 float b = colorObj["b"]?.ToObject<float>() ?? 1f;
//                 float a = firstFill["opacity"]?.ToObject<float>() ?? 1f;

//                 image.color = new Color(r, g, b, a);
//             }
//         }
//     }

//     private void ApplyTextFills(JArray fills, TextMeshProUGUI tmpText)
//     {
//         if (fills == null || fills.Count == 0) return;

//         JObject firstFill = fills[0] as JObject;
//         if (firstFill == null) return;

//         string fillType = firstFill["type"]?.ToString();

//         if (fillType == "SOLID")
//         {
//             JObject colorObj = firstFill["color"] as JObject;
//             if (colorObj != null)
//             {
//                 float r = colorObj["r"]?.ToObject<float>() ?? 0f;
//                 float g = colorObj["g"]?.ToObject<float>() ?? 0f;
//                 float b = colorObj["b"]?.ToObject<float>() ?? 0f;
//                 float a = firstFill["opacity"]?.ToObject<float>() ?? 1f;

//                 tmpText.color = new Color(r, g, b, a);
//             }
//         }
//     }

//     private void ApplyTextStyling(JObject nodeData, TextMeshProUGUI tmpText)
//     {
//         JObject style = nodeData["style"] as JObject;
//         if (style == null) return;

//         // Font size
//         float fontSize = style["fontSize"]?.ToObject<float>() ?? 16f;
//         tmpText.fontSize = fontSize * scaleFactor;

//         // Text alignment
//         string textAlignHorizontal = style["textAlignHorizontal"]?.ToString();
//         string textAlignVertical = style["textAlignVertical"]?.ToString();

//         TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;

//         switch (textAlignHorizontal?.ToUpper())
//         {
//             case "CENTER":
//                 alignment = TextAlignmentOptions.Top;
//                 break;
//             case "RIGHT":
//                 alignment = TextAlignmentOptions.TopRight;
//                 break;
//             case "JUSTIFIED":
//                 alignment = TextAlignmentOptions.TopJustified;
//                 break;
//         }

//         switch (textAlignVertical?.ToUpper())
//         {
//             case "CENTER":
//                 if (textAlignHorizontal?.ToUpper() == "CENTER")
//                     alignment = TextAlignmentOptions.Center;
//                 else if (textAlignHorizontal?.ToUpper() == "RIGHT")
//                     alignment = TextAlignmentOptions.Right;
//                 else
//                     alignment = TextAlignmentOptions.Left;
//                 break;
//             case "BOTTOM":
//                 if (textAlignHorizontal?.ToUpper() == "CENTER")
//                     alignment = TextAlignmentOptions.Bottom;
//                 else if (textAlignHorizontal?.ToUpper() == "RIGHT")
//                     alignment = TextAlignmentOptions.BottomRight;
//                 else
//                     alignment = TextAlignmentOptions.BottomLeft;
//                 break;
//         }

//         tmpText.alignment = alignment;

//         // Font weight (approximate)
//         float fontWeight = style["fontWeight"]?.ToObject<float>() ?? 400f;
//         if (fontWeight >= 700)
//         {
//             tmpText.fontStyle = FontStyles.Bold;
//         }
//         else if (fontWeight >= 500)
//         {
//             tmpText.fontStyle = FontStyles.Normal;
//         }
//     }

//     private void ApplyCornerRadius(JObject nodeData, GameObject gameObject)
//     {
//         float cornerRadius = nodeData["cornerRadius"]?.ToObject<float>() ?? 0f;

//         if (cornerRadius > 0)
//         {
//             // For simple rounded corners, we might need a custom shader or use UI shapes
//             // For now, we'll just log it
//             if (enableDebugLogs)
//                 Debug.Log($"Corner radius {cornerRadius} applied to {gameObject.name}");
//         }
//     }

//     private void SaveCanvasToPrefab()
//     {
//         if (targetCanvas == null) return;

// #if UNITY_EDITOR
//         string prefabSavePath = "Assets/Prefabs/FigmaUI/";

//         // Ensure directory exists
//         if (!Directory.Exists(prefabSavePath))
//         {
//             Directory.CreateDirectory(prefabSavePath);
//             AssetDatabase.Refresh();
//         }

//         string prefabPath = Path.Combine(prefabSavePath, $"{canvasName}.prefab");

//         GameObject prefab = PrefabUtility.SaveAsPrefabAsset(targetCanvas.gameObject, prefabPath);

//         if (prefab != null)
//         {
//             Debug.Log($"✓ Saved prefab: {prefabPath}");
//         }
//         else
//         {
//             Debug.LogError($"✗ Failed to save prefab: {prefabPath}");
//         }
// #endif
//     }

//     [ContextMenu("Convert Figma Node to UI")]
//     public void ConvertFigmaNodeToUI()
//     {
//         if (string.IsNullOrEmpty(node_convert))
//         {
//             Debug.LogError("node_convert is not set. Please set the node ID to convert.");
//             return;
//         }

//         StartCoroutine(ConvertFigmaToUI());
//     }

//     [ContextMenu("Clear Created UI")]
//     public void ClearCreatedUI()
//     {
//         foreach (var kvp in createdNodes)
//         {
//             if (kvp.Value != null)
//             {
//                 DestroyImmediate(kvp.Value);
//             }
//         }
//         createdNodes.Clear();

//         if (enableDebugLogs)
//             Debug.Log("✓ Cleared all created UI elements");
//     }

//     [ContextMenu("Save Canvas to Prefab")]
//     public void SaveCanvasToPrefabContext()
//     {
//         SaveCanvasToPrefab();
//     }

//     [ContextMenu("Debug JSON Structure")]
//     public void DebugJSONStructure()
//     {
//         if (string.IsNullOrEmpty(node_convert))
//         {
//             Debug.LogError("node_convert is not set.");
//             return;
//         }

//         string cachedFilePath = Path.Combine(Application.dataPath, "Resources", "FigmaData",
//             $"figma_node_{node_convert.Replace(":", "-")}.json");

//         if (!File.Exists(cachedFilePath))
//         {
//             Debug.LogError($"Figma data not found at: {cachedFilePath}");
//             return;
//         }

//         try
//         {
//             string jsonData = File.ReadAllText(cachedFilePath);
//             JObject root = JObject.Parse(jsonData);

//             Debug.Log("=== JSON Structure Debug ===");
//             Debug.Log($"Root keys: {string.Join(", ", root.Properties().Select(p => p.Name))}");

//             if (root["nodes"] != null)
//             {
//                 Debug.Log($"Nodes found: {root["nodes"].Children().Count()}");
//                 foreach (var node in root["nodes"].Take(3)) // Show first 3
//                 {
//                     if (node is JProperty prop)
//                     {
//                         var nodeObj = prop.Value as JObject;
//                         if (nodeObj != null)
//                         {
//                             Debug.Log($"Node {prop.Name}: type={nodeObj["type"]}, name={nodeObj["name"]}, id={nodeObj["id"]}");
//                         }
//                     }
//                 }
//             }

//             if (root["document"] != null)
//             {
//                 Debug.Log("Document structure found");
//                 var doc = root["document"] as JObject;
//                 if (doc != null)
//                 {
//                     Debug.Log($"Document: type={doc["type"]}, name={doc["name"]}, id={doc["id"]}");
//                 }
//             }

//             Debug.Log("=== End JSON Debug ===");
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError($"JSON Debug Error: {ex.Message}");
//         }
//     }

//     [ContextMenu("Validate Setup")]
//     public void ValidateSetup()
//     {
//         Debug.Log("=== FigmaToUIConverter Setup Validation ===");

//         // Check node_convert
//         if (string.IsNullOrEmpty(node_convert))
//         {
//             Debug.LogError("❌ node_convert is not set");
//         }
//         else
//         {
//             Debug.Log($"✅ Node ID configured: {node_convert}");
//         }

//         // Check Canvas
//         if (targetCanvas == null && !createNewCanvas)
//         {
//             Debug.LogError("❌ No target canvas and createNewCanvas is disabled");
//         }
//         else
//         {
//             Debug.Log("✅ Canvas configuration OK");
//         }

//         // Check Font
//         if (defaultFont == null)
//         {
//             Debug.LogWarning("⚠️ No default TextMeshPro font assigned");
//         }
//         else
//         {
//             Debug.Log($"✅ Default font assigned: {defaultFont.name}");
//         }

//         // Check cached data
//         if (!string.IsNullOrEmpty(node_convert))
//         {
//             string cachedFilePath = Path.Combine(Application.dataPath, "Resources", "FigmaData",
//                 $"figma_node_{node_convert.Replace(":", "-")}.json");

//             if (File.Exists(cachedFilePath))
//             {
//                 Debug.Log("✅ Cached Figma data found");
//             }
//             else
//             {
//                 Debug.LogWarning("⚠️ No cached data found. Download Figma data first.");
//             }
//         }

//         Debug.Log("=== End Validation ===");
//     }
// }
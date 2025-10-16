// using UnityEngine.Networking;
// using UnityEngine;
// using System.Collections;
// using System.IO;
// using Newtonsoft.Json.Linq;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using System.Threading;

// public class FigmaDownloader : MonoBehaviour
// {
//     [Header("Figma API Settings")]
//     public string figmaToken = "YOUR_FIGMA_TOKEN";
//     public string fileId = "YOUR_FILE_ID";
//     public string nodeId = "YOUR_NODE_ID";

//     [Header("Download Settings")]
//     public string imageFormat = "png";
//     public float imageScale = 1f;

//     [Header("Image Detection Settings")]
//     public bool useImagePrefix = true;

//     private FigmaApi _figmaApi;
//     private string ResourcesPath => Path.Combine(Application.dataPath, "Resources", "FigmaData");
//     // private string SpritesPath => Path.Combine(Application.dataPath, "Sprites");



//     private void EnsureDirectory(string path)
//     {
//         if (!Directory.Exists(path))
//             Directory.CreateDirectory(path);
//     }

//     private string GetNodeFilePath() => Path.Combine(ResourcesPath, $"figma_node_{nodeId.Replace(":", "-")}.json");

//     [ContextMenu("Download Node + Images")]
//     public void DownloadNodeAndImagesContext()
//     {
//         StartCoroutine(DownloadNodeAndImages());
//     }


//     private IEnumerator DownloadNodeAndImages()
//     {
//         EnsureDirectory(ResourcesPath);
//         // EnsureDirectory(SpritesPath);

//         string nodeFilePath = GetNodeFilePath();

//         // Download node JSON using UnityWebRequest (since FigmaApi doesn't have GetFileNodes method yet)
//         string encodedNodeId = UnityWebRequest.EscapeURL(nodeId);
//         string url = $"https://api.figma.com/v1/files/{fileId}/nodes?ids={encodedNodeId}";
//         UnityWebRequest www = UnityWebRequest.Get(url);
//         www.SetRequestHeader("X-FIGMA-TOKEN", figmaToken);

//         yield return www.SendWebRequest();

//         if (www.result == UnityWebRequest.Result.Success)
//         {
//             File.WriteAllText(nodeFilePath, www.downloadHandler.text);
//             Debug.Log($"✓ Node JSON saved: {nodeFilePath}");
//         }
//         else
//         {
//             Debug.LogError($"✗ Failed to download node: {www.error}");
//             yield break;
//         }

//         // Parse node JSON to collect image refs
//         string jsonContent = File.ReadAllText(nodeFilePath);
//         JObject root = JObject.Parse(jsonContent);

//         JToken nodeData = root["nodes"]?[nodeId]?["document"];
//         if (nodeData == null)
//         {
//             Debug.LogError($"Could not find node data for nodeId: {nodeId}");
//             yield break;
//         }

//         Debug.Log($"Found node document, starting image collection...");
//         List<string> imageNodeIds = new List<string>();
//         CollectImageNodeIds(nodeData, imageNodeIds);

//         if (imageNodeIds.Count == 0)
//         {
//             Debug.Log("No image nodes found to download.");
//             yield break;
//         }

//         // Download images using FigmaApi
//         yield return DownloadImagesUsingApi(imageNodeIds, nodeData);

//         Debug.Log("✓ Node + Images download completed.");
//     }

//     private IEnumerator DownloadImagesUsingApi(List<string> imageNodeIds, JToken nodeData)
//     {
//         //new 
//         _figmaApi = new FigmaApi(figmaToken);
//         // Create ImageRequestga
//         var imageRequest = new ImageRequest(fileId)
//         {
//             ids = imageNodeIds.ToArray(),
//             format = imageFormat,
//             scale = imageScale,
//             useAbsoluteBounds = true
//         };

//         // Use Task to call async method
//         Task<Dictionary<string, byte[]>> task = null;

//         try
//         {
//             task = _figmaApi.GetImageAsync(imageRequest, CancellationToken.None);
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError($"✗ Failed to start image download: {e.Message}");
//             yield break;
//         }

//         // Wait for task to complete
//         while (!task.IsCompleted)
//         {
//             yield return null;
//         }

//         if (task.IsFaulted)
//         {
//             Debug.LogError($"✗ Failed to download images: {task.Exception?.GetBaseException().Message}");
//             yield break;
//         }

//         var images = task.Result;
//         if (images == null || images.Count == 0)
//         {
//             Debug.LogWarning("No images were downloaded.");
//             yield break;
//         }

//         // Save downloaded images
//         string resourcesSpritesPath = Path.Combine(
//             Application.dataPath,
//             "Resources",
//             Constant.SAVE_IMAGE_FOLDER,
//             nodeId.Replace(":", "-")
//         );

//         EnsureDirectory(resourcesSpritesPath);

//         foreach (var kvp in images)
//         {
//             string imageNodeId = kvp.Key;
//             byte[] imageData = kvp.Value;

//             if (imageData == null)
//             {
//                 Debug.LogWarning($"⚠ Image node {imageNodeId} returned null data");
//                 continue;
//             }

//             // Find the node name for this ID to create a better filename
//             string nodeName = FindNodeNameById(nodeData, imageNodeId) ?? imageNodeId;
//             string fileName = nodeName.SanitizeFileName();
//             string filePath = Path.Combine(resourcesSpritesPath, $"{fileName}.{imageFormat}");

//             File.WriteAllBytes(filePath, imageData);
//             Debug.Log($"✓ Saved image: {fileName}.{imageFormat}");
//         }

// #if UNITY_EDITOR
//         // Refresh the asset database so Unity recognizes the new sprites
//         UnityEditor.AssetDatabase.Refresh();
// #endif
//     }


//     private void CollectImageNodeIds(JToken token, List<string> imageNodeIds)
//     {
//         if (token == null) return;

//         if (token.Type == JTokenType.Object)
//         {
//             var obj = (JObject)token;
//             string nodeId = obj["id"]?.ToString();
//             string nodeName = obj["name"]?.ToString();
//             bool hasImage = false;

//             // Check if node name has the specified prefix
//             if (useImagePrefix && !string.IsNullOrEmpty(nodeName) && !string.IsNullOrEmpty(Constant.IMAGE_PREFIX) && nodeName.StartsWith(Constant.IMAGE_PREFIX))
//             {
//                 hasImage = true;
//                 // Debug.Log($"Found node with '{imagePrefix}' prefix: {nodeName} (ID: {nodeId})");
//             }

//             if (hasImage && !string.IsNullOrEmpty(nodeId) && !imageNodeIds.Contains(nodeId))
//             {

//                 // Debug.Log($"Added image node to download list: {nodeName} ({nodeId})");
//                 imageNodeIds.Add(nodeId);
//             }

//             // Recursively check children
//             if (obj.TryGetValue("children", out JToken childrenToken))
//             {
//                 CollectImageNodeIds(childrenToken, imageNodeIds);
//             }
//         }
//         else if (token.Type == JTokenType.Array)
//         {
//             foreach (var child in (JArray)token)
//                 CollectImageNodeIds(child, imageNodeIds);
//         }
//     }


//     private string FindNodeNameById(JToken token, string targetId)
//     {
//         if (token == null) return null;

//         if (token.Type == JTokenType.Object)
//         {
//             var obj = (JObject)token;
//             string nodeId = obj["id"]?.ToString();

//             if (nodeId == targetId)
//             {
//                 return obj["name"]?.ToString();
//             }

//             // Recursively check children
//             if (obj.TryGetValue("children", out JToken childrenToken))
//             {
//                 string result = FindNodeNameById(childrenToken, targetId);
//                 if (result != null) return result;
//             }
//         }
//         else if (token.Type == JTokenType.Array)
//         {
//             foreach (var child in (JArray)token)
//             {
//                 string result = FindNodeNameById(child, targetId);
//                 if (result != null) return result;
//             }
//         }

//         return null;
//     }
// }


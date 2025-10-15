using UnityEngine.Networking;
using UnityEngine;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

public class FigmaDownloader : MonoBehaviour
{
    [Header("Figma API Settings")]
    public string figmaToken = "YOUR_FIGMA_TOKEN";
    public string fileId = "YOUR_FILE_ID";
    public string nodeId = "YOUR_NODE_ID";

    [Header("Download Settings")]
    public string imageFormat = "png"; // png, jpg
    public float imageScale = 1f; // 0.5 to 4.0

    private string ResourcesPath => Path.Combine(Application.dataPath, "Resources", "FigmaData");
    private string SpritesPath => Path.Combine(Application.dataPath, "Sprites");

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private string GetNodeFilePath() => Path.Combine(ResourcesPath, $"figma_node_{nodeId.Replace(":", "-")}.json");

    [ContextMenu("Download Node + Images")]
    public void DownloadNodeAndImagesContext()
    {
        StartCoroutine(DownloadNodeAndImages());
    }

    private IEnumerator DownloadNodeAndImages()
    {
        EnsureDirectory(ResourcesPath);
        EnsureDirectory(SpritesPath);

        string nodeFilePath = GetNodeFilePath();

        // Download node JSON
        string encodedNodeId = UnityWebRequest.EscapeURL(nodeId);
        string url = $"https://api.figma.com/v1/files/{fileId}/nodes?ids={encodedNodeId}";
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("X-FIGMA-TOKEN", figmaToken);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            File.WriteAllText(nodeFilePath, www.downloadHandler.text);
            Debug.Log($"✓ Node JSON saved: {nodeFilePath}");
        }
        else
        {
            Debug.LogError($"✗ Failed to download node: {www.error}");
            yield break;
        }

        // Parse node JSON to collect image refs
        string jsonContent = File.ReadAllText(nodeFilePath);
        Debug.Log($"JSON file size: {jsonContent.Length} characters");

        JObject root = JObject.Parse(jsonContent);
        Debug.Log($"Parsed JSON root keys: {string.Join(", ", root.Properties().Select(p => p.Name))}");

        // Navigate to the actual node data
        JToken nodeData = root["nodes"]?[nodeId]?["document"];
        if (nodeData == null)
        {
            Debug.LogError($"Could not find node data for nodeId: {nodeId}");
            yield break;
        }

        Debug.Log($"Found node document, starting image collection...");
        List<string> imageNodeIds = new List<string>();
        CollectImageNodeIds(nodeData, imageNodeIds);

        if (imageNodeIds.Count == 0)
        {
            Debug.Log("No images found in node.");
            yield break;
        }

        Debug.Log($"Found {imageNodeIds.Count} image node(s) to download:");
        foreach (string nodeId in imageNodeIds)
        {
            Debug.Log($"  - Image node ID: {nodeId}");
        }

        // Download images
        string refsParam = string.Join(",", imageNodeIds);
        string imagesUrl = $"https://api.figma.com/v1/images/{fileId}?ids={refsParam}&format={imageFormat}&scale={imageScale}";

        Debug.Log($"Making image URL request: {imagesUrl}");
        Debug.Log($"Image refs parameter: {refsParam}");

        UnityWebRequest imgReq = UnityWebRequest.Get(imagesUrl);
        imgReq.SetRequestHeader("X-FIGMA-TOKEN", figmaToken);

        yield return imgReq.SendWebRequest();

        if (imgReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"✗ Failed to get image URLs: {imgReq.error}");
            Debug.LogError($"Response: {imgReq.downloadHandler.text}");
            yield break;
        }

        JObject imagesResponse = JObject.Parse(imgReq.downloadHandler.text);
        JObject imagesDict = imagesResponse["images"] as JObject;

        foreach (var kvp in imagesDict)
        {
            string refId = kvp.Key;
            string imageUrl = kvp.Value?.ToString();
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl != "null")
            {
                yield return DownloadSingleImage(refId, imageUrl);
            }
        }

        Debug.Log("✓ Node + Images download completed.");
    }

    private void CollectImageNodeIds(JToken token, List<string> imageNodeIds)
    {
        if (token == null) return;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();
            bool hasImage = false;

            // Check fills array for images
            if (obj.TryGetValue("fills", out JToken fillsToken) && fillsToken is JArray fills)
            {
                foreach (var fillToken in fills)
                {
                    if (fillToken["type"]?.ToString() == "IMAGE")
                    {
                        hasImage = true;
                        break;
                    }
                }
            }

            // Check background array for images
            if (!hasImage && obj.TryGetValue("background", out JToken bgToken) && bgToken is JArray backgrounds)
            {
                foreach (var bg in backgrounds)
                {
                    if (bg["type"]?.ToString() == "IMAGE")
                    {
                        hasImage = true;
                        break;
                    }
                }
            }

            // If this node has images, add its ID
            if (hasImage && !string.IsNullOrEmpty(nodeId) && !imageNodeIds.Contains(nodeId))
            {
                Debug.Log($"Found image node: {nodeId}");
                imageNodeIds.Add(nodeId);
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


    private IEnumerator DownloadSingleImage(string refId, string imageUrl)
    {
        string filePath = Path.Combine(SpritesPath, $"{refId}.{imageFormat}");
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] data = www.downloadHandler.data;
            File.WriteAllBytes(filePath, data);
            Debug.Log($"✓ Downloaded image: {filePath}");
        }
        else
        {
            Debug.LogError($"✗ Failed to download image {refId}: {www.error}");
        }
    }
}

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
    public string imageFormat = "png";
    public float imageScale = 1f;

    [Header("Image Detection Settings")]
    public bool useImagePrefix = true;

    private string ResourcesPath => Path.Combine(Application.dataPath, "Resources", "FigmaData");
    // private string SpritesPath => Path.Combine(Application.dataPath, "Sprites");

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

    [ContextMenu("Preview Image Nodes")]
    public void PreviewImageNodes()
    {
        StartCoroutine(PreviewImageNodesCoroutine());
    }

    private IEnumerator PreviewImageNodesCoroutine()
    {
        string nodeFilePath = GetNodeFilePath();

        if (!File.Exists(nodeFilePath))
        {
            Debug.LogError($"Node file not found: {nodeFilePath}. Please download the node first.");
            yield break;
        }

        string jsonContent = File.ReadAllText(nodeFilePath);
        JObject root = JObject.Parse(jsonContent);
        JToken nodeData = root["nodes"]?[nodeId]?["document"];

        if (nodeData == null)
        {
            Debug.LogError($"Could not find node data for nodeId: {nodeId}");
            yield break;
        }

        List<string> imageNodeIds = new List<string>();
        CollectImageNodeIds(nodeData, imageNodeIds);

        Debug.Log("=== Image Nodes Preview ===");
        Debug.Log($"Image prefix detection: {(useImagePrefix ? "ENABLED" : "DISABLED")} (prefix: '{Constant.IMAGE_PREFIX}')");
        Debug.Log($"Found {imageNodeIds.Count} image node(s) that would be downloaded:");

        foreach (string id in imageNodeIds)
        {
            string name = FindNodeNameById(nodeData, id);
            Debug.Log($"  • {name} (ID: {id})");
        }

        Debug.Log("=== End Preview ===");
        yield return null;
    }

    private IEnumerator DownloadNodeAndImages()
    {
        EnsureDirectory(ResourcesPath);
        // EnsureDirectory(SpritesPath);

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
        JObject root = JObject.Parse(jsonContent);

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
            yield break;
        }

        // Download images
        string refsParam = string.Join(",", imageNodeIds);
        string imagesUrl = $"https://api.figma.com/v1/images/{fileId}?ids={refsParam}&format={imageFormat}&scale={imageScale}&use_absolute_bounds=true";


        UnityWebRequest imgReq = UnityWebRequest.Get(imagesUrl);
        imgReq.SetRequestHeader("X-FIGMA-TOKEN", figmaToken);

        yield return imgReq.SendWebRequest();

        if (imgReq.result != UnityWebRequest.Result.Success)
        {
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
                // Find the node name for this ID to create a better filename
                string nodeName = FindNodeNameById(nodeData, refId) ?? refId;
                yield return DownloadSingleImage(refId, imageUrl, nodeName);
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
            string nodeName = obj["name"]?.ToString();
            bool hasImage = false;

            // Check if node name has the specified prefix
            if (useImagePrefix && !string.IsNullOrEmpty(nodeName) && !string.IsNullOrEmpty(Constant.IMAGE_PREFIX) && nodeName.StartsWith(Constant.IMAGE_PREFIX))
            {
                hasImage = true;
                // Debug.Log($"Found node with '{imagePrefix}' prefix: {nodeName} (ID: {nodeId})");
            }

            if (hasImage && !string.IsNullOrEmpty(nodeId) && !imageNodeIds.Contains(nodeId))
            {

                // Debug.Log($"Added image node to download list: {nodeName} ({nodeId})");
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


    private string FindNodeNameById(JToken token, string targetId)
    {
        if (token == null) return null;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeId = obj["id"]?.ToString();

            if (nodeId == targetId)
            {
                return obj["name"]?.ToString();
            }

            // Recursively check children
            if (obj.TryGetValue("children", out JToken childrenToken))
            {
                string result = FindNodeNameById(childrenToken, targetId);
                if (result != null) return result;
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var child in (JArray)token)
            {
                string result = FindNodeNameById(child, targetId);
                if (result != null) return result;
            }
        }

        return null;
    }

    private IEnumerator DownloadSingleImage(string refId, string imageUrl, string nodeName = null)
    {
        // Create filename from node name if available, otherwise use refId
        string fileName = !string.IsNullOrEmpty(nodeName) ? nodeName : refId;
        fileName = fileName.SanitizeFileName();

        // Use Constant.SAVE_IMAGE_FOLDER for the folder name
        string resourcesSpritesPath = Path.Combine(
            Application.dataPath,
            "Resources",
            Constant.SAVE_IMAGE_FOLDER,
            nodeId.Replace(":", "-")
        );

        // Ensure the directory exists
        EnsureDirectory(resourcesSpritesPath);

        string filePath = Path.Combine(resourcesSpritesPath, $"{fileName}.{imageFormat}");

        Debug.Log($"Downloading image to: {filePath}");

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] data = www.downloadHandler.data;
            File.WriteAllBytes(filePath, data);
            // Debug.Log($"✓ Downloaded image: {filePath} (Node: {nodeName ?? refId})");

#if UNITY_EDITOR
            // Refresh the asset database so Unity recognizes the new sprite
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        else
        {
            Debug.LogError($"✗ Failed to download image {refId}: {www.error}");
        }
    }



    // private string SanitizeFileName(string fileName)
    // {
    //     if (string.IsNullOrEmpty(fileName)) return "unknown";

    //     // Remove invalid filename characters
    //     char[] invalidChars = Path.GetInvalidFileNameChars();
    //     foreach (char c in invalidChars)
    //     {
    //         fileName = fileName.Replace(c, '_');
    //     }

    //     // Replace spaces with underscores
    //     fileName = fileName.Replace(' ', '_');

    //     return fileName;
    // }
}


// using UnityEngine;

// /// <summary>
// /// Test utility for Figma URL extraction
// /// </summary>
// public class FigmaUrlExtractor : MonoBehaviour
// {
//     [ContextMenu("Test URL Extraction")]
//     public void TestUrlExtraction()
//     {
//         string testUrl = "https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15&m=dev";
        
//         Debug.Log($"Testing URL: {testUrl}");
        
//         var result = ExtractFileAndNodeIds(testUrl);
//         if (result != null)
//         {
//             Debug.Log($"✅ Success!");
//             Debug.Log($"  File ID: {result.Value.fileId}");
//             Debug.Log($"  Node ID: {result.Value.nodeId}");
//         }
//         else
//         {
//             Debug.LogError("❌ Failed to extract IDs");
//         }
//     }

//     /// <summary>
//     /// Extracts file ID and node ID from a Figma URL
//     /// </summary>
//     private (string fileId, string nodeId)? ExtractFileAndNodeIds(string url)
//     {
//         if (string.IsNullOrEmpty(url))
//             return null;

//         try
//         {
//             url = url.Trim();

//             if (!url.Contains("figma.com"))
//             {
//                 Debug.LogError("Invalid Figma URL. URL must contain 'figma.com'");
//                 return null;
//             }

//             string extractedFileId = "";
//             string extractedNodeId = "";

//             // Extract file ID from URL path
//             var fileIdMatch = System.Text.RegularExpressions.Regex.Match(url, @"/(?:design|file)/([A-Za-z0-9]+)/");
//             if (fileIdMatch.Success)
//             {
//                 extractedFileId = fileIdMatch.Groups[1].Value;
//             }
//             else
//             {
//                 Debug.LogError("Could not extract file ID from URL. Expected format: /design/FILE_ID/ or /file/FILE_ID/");
//                 return null;
//             }

//             // Extract node ID from query parameters
//             var nodeIdMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]node-id=([^&]+)");
//             if (nodeIdMatch.Success)
//             {
//                 extractedNodeId = nodeIdMatch.Groups[1].Value;
//                 // Convert URL encoded colon (%3A) back to colon
//                 extractedNodeId = extractedNodeId.Replace("%3A", ":");
//                 // Convert dash to colon if it's in the format 1001-15
//                 if (extractedNodeId.Contains("-") && !extractedNodeId.Contains(":"))
//                 {
//                     extractedNodeId = extractedNodeId.Replace("-", ":");
//                 }
//             }
//             else
//             {
//                 Debug.LogWarning("No node ID found in URL.");
//                 return null;
//             }

//             if (string.IsNullOrEmpty(extractedFileId))
//             {
//                 Debug.LogError("Failed to extract file ID from URL");
//                 return null;
//             }

//             return (extractedFileId, extractedNodeId);
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError($"Error parsing Figma URL: {ex.Message}");
//             return null;
//         }
//     }
// }

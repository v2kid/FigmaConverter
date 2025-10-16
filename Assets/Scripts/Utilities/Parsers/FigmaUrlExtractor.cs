using UnityEngine;

/// <summary>
/// Utility for extracting file IDs and node IDs from Figma URLs
/// </summary>
public static class FigmaUrlExtractor
{
    /// <summary>
    /// Extracts file ID and node ID from a Figma URL
    /// </summary>
    public static (string fileId, string nodeId)? ExtractFileAndNodeIds(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            url = url.Trim();

            if (!url.Contains("figma.com"))
            {
                Debug.LogError("Invalid Figma URL. URL must contain 'figma.com'");
                return null;
            }

            string extractedFileId = "";
            string extractedNodeId = "";

            // Extract file ID from URL path
            var fileIdMatch = System.Text.RegularExpressions.Regex.Match(
                url,
                @"/(?:design|file)/([A-Za-z0-9]+)/"
            );

            if (fileIdMatch.Success)
            {
                extractedFileId = fileIdMatch.Groups[1].Value;
            }
            else
            {
                Debug.LogError(
                    "Could not extract file ID from URL. Expected format: /design/FILE_ID/ or /file/FILE_ID/"
                );
                return null;
            }

            // Extract node ID from query parameters
            var nodeIdMatch = System.Text.RegularExpressions.Regex.Match(
                url,
                @"[?&]node-id=([^&]+)"
            );

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
                Debug.LogWarning("No node ID found in URL.");
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
}

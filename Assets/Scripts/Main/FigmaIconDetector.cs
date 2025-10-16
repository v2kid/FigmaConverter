using Newtonsoft.Json.Linq;

/// <summary>
/// Detects if a FRAME/GROUP is actually an icon (contains only vector children)
/// Icons will be downloaded via Figma Image API instead of being generated from paths
/// </summary>
public static class FigmaIconDetector
{
    /// <summary>
    /// Checks if a node is an icon frame (contains only vector children)
    /// These frames should be downloaded as images via Figma Image API
    /// </summary>
    public static bool IsIconFrame(JObject nodeData)
    {
        string nodeType = nodeData["type"]?.ToString()?.ToUpper();
        
        // Must be a FRAME, GROUP, or COMPONENT
        if (nodeType != "FRAME" && nodeType != "GROUP" && nodeType != "COMPONENT")
            return false;

        JArray children = nodeData["children"] as JArray;
        if (children == null || children.Count == 0)
            return false;

        // Check if ALL children are vector types
        bool hasVectorChildren = false;
        
        foreach (JObject child in children)
        {
            string childType = child["type"]?.ToString()?.ToUpper();
            
            // Vector types that make up icons
            if (childType == "VECTOR" || childType == "STAR" || 
                childType == "POLYGON" || childType == "BOOLEAN_OPERATION" ||
                childType == "LINE")
            {
                hasVectorChildren = true;
            }
            // If it's a FRAME/GROUP, check recursively
            else if (childType == "FRAME" || childType == "GROUP" || childType == "COMPONENT")
            {
                if (IsIconFrame(child))
                {
                    hasVectorChildren = true;
                }
                else
                {
                    // Has non-icon frame children, not a pure icon
                    return false;
                }
            }
            // Has non-vector children (TEXT, RECTANGLE, IMAGE, etc.)
            else if (childType != null)
            {
                return false;
            }
        }

        return hasVectorChildren;
    }

    /// <summary>
    /// Counts the number of vector children in an icon frame
    /// Useful for logging/debugging
    /// </summary>
    public static int CountVectorChildren(JObject iconFrame)
    {
        int count = 0;
        
        JArray children = iconFrame["children"] as JArray;
        if (children != null)
        {
            foreach (JObject child in children)
            {
                string childType = child["type"]?.ToString()?.ToUpper();
                
                if (childType == "VECTOR" || childType == "STAR" || 
                    childType == "POLYGON" || childType == "BOOLEAN_OPERATION" ||
                    childType == "LINE")
                {
                    count++;
                }
                else if (childType == "FRAME" || childType == "GROUP" || childType == "COMPONENT")
                {
                    if (IsIconFrame(child))
                    {
                        count += CountVectorChildren(child);
                    }
                }
            }
        }
        
        return count;
    }

    /// <summary>
    /// Gets a summary of an icon frame for logging
    /// </summary>
    public static string GetIconFrameSummary(JObject iconFrame)
    {
        string name = iconFrame["name"]?.ToString() ?? "Unknown";
        string id = iconFrame["id"]?.ToString() ?? "";
        int vectorCount = CountVectorChildren(iconFrame);
        
        JObject bounds = iconFrame["absoluteBoundingBox"] as JObject;
        float width = bounds?["width"]?.ToObject<float>() ?? 0f;
        float height = bounds?["height"]?.ToObject<float>() ?? 0f;
        
        return $"Icon: {name} (ID: {id}, {vectorCount} vectors, {width}x{height}px)";
    }
}

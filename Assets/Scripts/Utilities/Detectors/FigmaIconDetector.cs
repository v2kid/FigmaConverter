using Newtonsoft.Json.Linq;

public static class FigmaIconDetector
{
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
            if (
                childType == "VECTOR"
                || childType == "STAR"
                || childType == "POLYGON"
                || childType == "BOOLEAN_OPERATION"
                || childType == "LINE"
            )
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
}

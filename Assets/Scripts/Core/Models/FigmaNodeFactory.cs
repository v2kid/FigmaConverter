using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FigmaNodeFactory
{
    public static FigmaNode CreateNode(JObject jsonObject)
    {
        if (!jsonObject.TryGetValue("type", out JToken typeToken))
        {
            Debug.LogError("Node missing type field");
            return null;
        }

        string typeString = typeToken.ToString();
        if (!Enum.TryParse<FigmaNodeType>(typeString, out FigmaNodeType nodeType))
        {
            Debug.LogWarning($"Unknown node type: {typeString}");
            return JsonConvert.DeserializeObject<FigmaNode>(jsonObject.ToString());
        }

        FigmaNode node = nodeType switch
        {
            FigmaNodeType.DOCUMENT => JsonConvert.DeserializeObject<FigmaDocumentNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.CANVAS => JsonConvert.DeserializeObject<FigmaCanvasNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.FRAME => JsonConvert.DeserializeObject<FigmaFrameNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.GROUP => JsonConvert.DeserializeObject<FigmaGroupNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.COMPONENT => JsonConvert.DeserializeObject<FigmaComponentNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.INSTANCE => JsonConvert.DeserializeObject<FigmaInstanceNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.COMPONENT_SET => JsonConvert.DeserializeObject<FigmaComponentSetNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.RECTANGLE => JsonConvert.DeserializeObject<FigmaRectangleNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.TEXT => JsonConvert.DeserializeObject<FigmaTextNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.ELLIPSE => JsonConvert.DeserializeObject<FigmaEllipseNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.LINE => JsonConvert.DeserializeObject<FigmaLineNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.VECTOR => JsonConvert.DeserializeObject<FigmaVectorNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.STAR => JsonConvert.DeserializeObject<FigmaStarNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.POLYGON => JsonConvert.DeserializeObject<FigmaPolygonNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.BOOLEAN_OPERATION =>
                JsonConvert.DeserializeObject<FigmaBooleanOperationNode>(jsonObject.ToString()),
            FigmaNodeType.SLICE => JsonConvert.DeserializeObject<FigmaSliceNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.SECTION => JsonConvert.DeserializeObject<FigmaSectionNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.WIDGET => JsonConvert.DeserializeObject<FigmaWidgetNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.EMBED => JsonConvert.DeserializeObject<FigmaEmbedNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.LINK_UNFURL => JsonConvert.DeserializeObject<FigmaLinkUnfurlNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.MEDIA => JsonConvert.DeserializeObject<FigmaMediaNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.TABLE => JsonConvert.DeserializeObject<FigmaTableNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.TABLE_CELL => JsonConvert.DeserializeObject<FigmaTableCellNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.STICKY => JsonConvert.DeserializeObject<FigmaStickyNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.SHAPE_WITH_TEXT => JsonConvert.DeserializeObject<FigmaShapeWithTextNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.CONNECTOR => JsonConvert.DeserializeObject<FigmaConnectorNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.WASHI_TAPE => JsonConvert.DeserializeObject<FigmaWashiTapeNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.CODE_BLOCK => JsonConvert.DeserializeObject<FigmaCodeBlockNode>(
                jsonObject.ToString()
            ),
            FigmaNodeType.STAMP => JsonConvert.DeserializeObject<FigmaStampNode>(
                jsonObject.ToString()
            ),
            _ => JsonConvert.DeserializeObject<FigmaNode>(jsonObject.ToString()),
        };

        // Process children recursively if they exist
        if (
            jsonObject.TryGetValue("children", out JToken childrenToken)
            && childrenToken is JArray childrenArray
        )
        {
            FigmaNode[] children = new FigmaNode[childrenArray.Count];
            for (int i = 0; i < childrenArray.Count; i++)
            {
                if (childrenArray[i] is JObject childObject)
                {
                    children[i] = CreateNode(childObject);
                }
            }
            node.children = children;
        }

        return node;
    }

    public static FigmaDocument ParseFigmaFile(string jsonString)
    {
        try
        {
            JObject rootObject = JObject.Parse(jsonString);
            return JsonConvert.DeserializeObject<FigmaDocument>(jsonString);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse Figma file: {ex.Message}");
            return null;
        }
    }
}

[Serializable]
public class FigmaDocument
{
    public string name;
    public string role;
    public string lastModified;
    public string editorType;
    public string thumbnailUrl;
    public string version;
    public FigmaDocumentNode document;
    public FigmaComponentMetadata components;
    public FigmaComponentSetMetadata componentSets;
    public int schemaVersion;
    public FigmaStyle[] styles;
}

[Serializable]
public class FigmaComponentMetadata
{
    // Key-value pairs of component ID to component metadata
}

[Serializable]
public class FigmaComponentSetMetadata
{
    // Key-value pairs of component set ID to component set metadata
}

[Serializable]
public class FigmaStyle
{
    public string key;
    public string name;
    public string styleType; // FILL, TEXT, EFFECT, GRID
    public string description;
    public bool remote;
}

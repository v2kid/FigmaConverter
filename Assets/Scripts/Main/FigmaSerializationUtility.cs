using System;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public static class FigmaSerializationUtility
{
    /// <summary>
    /// Serializes a Figma node tree to JSON
    /// </summary>
    public static string SerializeNode(FigmaNode node)
    {
        if (node == null) return null;

        try
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(node, settings);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to serialize Figma node: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deserializes JSON to a Figma node with the correct type
    /// </summary>
    public static FigmaNode DeserializeNode(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            JObject jsonObject = JObject.Parse(json);
            return FigmaNodeFactory.CreateNode(jsonObject);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to deserialize Figma node: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts raw Figma API response to structured data
    /// </summary>
    public static FigmaDocument ParseFigmaApiResponse(string apiResponse)
    {
        if (string.IsNullOrEmpty(apiResponse)) return null;

        try
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            // First parse as generic document
            FigmaDocument document = JsonConvert.DeserializeObject<FigmaDocument>(apiResponse, settings);

            // Then parse the document node with proper typing
            JObject root = JObject.Parse(apiResponse);
            JToken documentNode = root["document"];

            if (documentNode != null)
            {
                document.document = FigmaNodeFactory.CreateNode(documentNode as JObject) as FigmaDocumentNode;
            }

            return document;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse Figma API response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts all node types from a Figma document
    /// </summary>
    public static HashSet<string> GetAllNodeTypes(FigmaNode rootNode)
    {
        var nodeTypes = new HashSet<string>();
        CollectNodeTypes(rootNode, nodeTypes);
        return nodeTypes;
    }

    private static void CollectNodeTypes(FigmaNode node, HashSet<string> nodeTypes)
    {
        if (node == null) return;

        nodeTypes.Add(node.type.ToString());

        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                CollectNodeTypes(child, nodeTypes);
            }
        }
    }

    /// <summary>
    /// Validates that all required node types are supported
    /// </summary>
    public static bool ValidateNodeTypeSupport(string jsonResponse)
    {
        try
        {
            JObject root = JObject.Parse(jsonResponse);
            var foundTypes = new HashSet<string>();
            FindAllNodeTypesInJson(root, foundTypes);

            var supportedTypes = Enum.GetNames(typeof(FigmaNodeType));
            var supportedSet = new HashSet<string>(supportedTypes);

            bool allSupported = true;
            foreach (string foundType in foundTypes)
            {
                if (!supportedSet.Contains(foundType))
                {
                    Debug.LogWarning($"Unsupported node type found: {foundType}");
                    allSupported = false;
                }
            }

            Debug.Log($"Found {foundTypes.Count} node types, {(allSupported ? "all supported" : "some unsupported")}");
            return allSupported;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to validate node type support: {ex.Message}");
            return false;
        }
    }

    private static void FindAllNodeTypesInJson(JToken token, HashSet<string> nodeTypes)
    {
        if (token is JObject obj && obj["type"] != null)
        {
            nodeTypes.Add(obj["type"].ToString());
        }

        if (token is JArray array)
        {
            foreach (var item in array)
            {
                FindAllNodeTypesInJson(item, nodeTypes);
            }
        }
        else if (token is JObject objToken)
        {
            foreach (var property in objToken.Properties())
            {
                FindAllNodeTypesInJson(property.Value, nodeTypes);
            }
        }
    }

    /// <summary>
    /// Creates a summary of the Figma document structure
    /// </summary>
    public static FigmaDocumentSummary CreateDocumentSummary(FigmaDocument document)
    {
        if (document == null) return null;

        var summary = new FigmaDocumentSummary
        {
            name = document.name,
            lastModified = document.lastModified,
            version = document.version,
            nodeTypeCounts = new Dictionary<string, int>(),
            totalNodes = 0
        };

        if (document.document != null)
        {
            CountNodes(document.document, summary.nodeTypeCounts, ref summary.totalNodes);
        }

        return summary;
    }

    private static void CountNodes(FigmaNode node, Dictionary<string, int> counts, ref int total)
    {
        if (node == null) return;

        string nodeType = node.type.ToString();
        if (counts.ContainsKey(nodeType))
            counts[nodeType]++;
        else
            counts[nodeType] = 1;

        total++;

        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                CountNodes(child, counts, ref total);
            }
        }
    }
}

[Serializable]
public class FigmaDocumentSummary
{
    public string name;
    public string lastModified;
    public string version;
    public Dictionary<string, int> nodeTypeCounts;
    public int totalNodes;

    public void LogSummary()
    {
        Debug.Log($"=== Figma Document Summary ===");
        Debug.Log($"Name: {name}");
        Debug.Log($"Last Modified: {lastModified}");
        Debug.Log($"Version: {version}");
        Debug.Log($"Total Nodes: {totalNodes}");
        Debug.Log($"Node Type Breakdown:");

        foreach (var kvp in nodeTypeCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }
}
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Caches Figma node data to avoid redundant parsing and lookups.
/// No eviction: the cache is cleared explicitly after conversion finishes.
/// </summary>
public class NodeDataCacheService
{
    private readonly Dictionary<string, JObject> _nodeCache;
    private readonly Dictionary<string, string> _nodeNameCache;

    public int CachedNodeCount => _nodeCache.Count;

    public NodeDataCacheService()
    {
        _nodeCache = new Dictionary<string, JObject>();
        _nodeNameCache = new Dictionary<string, string>();
    }

    /// <summary>
    /// Adds a node to the cache
    /// </summary>
    public void AddNode(string nodeId, JObject nodeData)
    {
        if (string.IsNullOrEmpty(nodeId) || nodeData == null)
            return;

        _nodeCache[nodeId] = nodeData;

        string nodeName = nodeData["name"]?.ToString();
        if (!string.IsNullOrEmpty(nodeName))
            _nodeNameCache[nodeId] = nodeName;
    }

    /// <summary>
    /// Gets a node from cache by ID
    /// </summary>
    public JObject GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        return _nodeCache.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <summary>
    /// Gets a node name from cache by ID (faster than parsing JSON)
    /// </summary>
    public string GetNodeName(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        return _nodeNameCache.TryGetValue(nodeId, out var name) ? name : null;
    }

    /// <summary>
    /// Checks if a node exists in cache
    /// </summary>
    public bool ContainsNode(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _nodeCache.ContainsKey(nodeId);
    }

    /// <summary>
    /// Indexes all nodes in a tree for fast lookup
    /// </summary>
    public void IndexNodeTree(JToken root)
    {
        if (root == null)
            return;

        if (root.Type == JTokenType.Object)
        {
            var obj = (JObject)root;
            string nodeId = obj["id"]?.ToString();

            if (!string.IsNullOrEmpty(nodeId))
                AddNode(nodeId, obj);

            if (obj.TryGetValue("children", out JToken childrenToken) && childrenToken is JArray children)
            {
                foreach (var child in children)
                    IndexNodeTree(child);
            }
        }
        else if (root.Type == JTokenType.Array)
        {
            foreach (var item in (JArray)root)
                IndexNodeTree(item);
        }
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    public void Clear()
    {
        _nodeCache.Clear();
        _nodeNameCache.Clear();
    }
}

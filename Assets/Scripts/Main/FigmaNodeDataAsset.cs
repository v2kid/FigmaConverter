using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class FigmaNodeData
{
    public string nodeId;
    public string nodeName;
    public string nodeType;
    public TextAsset jsonData;

    [TextArea(3, 10)]
    public string description;

    public bool isValid => jsonData != null && !string.IsNullOrEmpty(nodeId);
}

[CreateAssetMenu(fileName = "FigmaNodeDataAsset", menuName = "Figma/Node Data Asset")]
public class FigmaNodeDataAsset : ScriptableObject
{
    [Header("Node Information")]
    public List<FigmaNodeData> nodeDataList = new List<FigmaNodeData>();

    [Header("Settings")]
    public bool autoParseOnValidate = true;
    public bool showDebugInfo = false;

    [Header("Read-Only Info")]
    [SerializeField] private int totalNodes = 0;
    [SerializeField] private int validNodes = 0;

    private Dictionary<string, JObject> parsedNodeCache = new Dictionary<string, JObject>();

    private void OnValidate()
    {
        if (autoParseOnValidate)
        {
            ValidateAndUpdateNodes();
        }

        UpdateStats();
    }

    public void ValidateAndUpdateNodes()
    {
        parsedNodeCache.Clear();

        foreach (var nodeData in nodeDataList)
        {
            if (nodeData.jsonData != null)
            {
                try
                {
                    JObject root = JObject.Parse(nodeData.jsonData.text);

                    // Try to extract node info from the JSON
                    if (string.IsNullOrEmpty(nodeData.nodeId))
                    {
                        // Try to find node ID from JSON structure
                        if (root["nodes"] != null)
                        {
                            var firstNode = root["nodes"].First;
                            if (firstNode is JProperty prop)
                            {
                                nodeData.nodeId = prop.Name;

                                var docNode = prop.Value["document"];
                                if (docNode != null)
                                {
                                    nodeData.nodeName = docNode["name"]?.ToString() ?? "Unknown";
                                    nodeData.nodeType = docNode["type"]?.ToString() ?? "Unknown";
                                }
                            }
                        }
                        else if (root["document"] != null)
                        {
                            var doc = root["document"];
                            nodeData.nodeId = doc["id"]?.ToString() ?? "Unknown";
                            nodeData.nodeName = doc["name"]?.ToString() ?? "Unknown";
                            nodeData.nodeType = doc["type"]?.ToString() ?? "Unknown";
                        }
                    }

                    if (!string.IsNullOrEmpty(nodeData.nodeId))
                    {
                        parsedNodeCache[nodeData.nodeId] = root;
                    }
                }
                catch (System.Exception ex)
                {
                    if (showDebugInfo)
                        Debug.LogError($"Failed to parse JSON for {nodeData.nodeId}: {ex.Message}");
                }
            }
        }
    }

    public JObject GetParsedNodeData(string nodeId)
    {
        if (parsedNodeCache.ContainsKey(nodeId))
        {
            return parsedNodeCache[nodeId];
        }

        // Try to find and parse on demand
        var nodeData = GetNodeData(nodeId);
        if (nodeData != null && nodeData.jsonData != null)
        {
            try
            {
                JObject root = JObject.Parse(nodeData.jsonData.text);
                parsedNodeCache[nodeId] = root;
                return root;
            }
            catch (System.Exception ex)
            {
                if (showDebugInfo)
                    Debug.LogError($"Failed to parse JSON for {nodeId}: {ex.Message}");
            }
        }

        return null;
    }

    public FigmaNodeData GetNodeData(string nodeId)
    {
        return nodeDataList.Find(data => data.nodeId == nodeId);
    }

    public JObject GetDocumentNode(string nodeId)
    {
        var root = GetParsedNodeData(nodeId);
        if (root == null) return null;

        // Try nodes structure first
        if (root["nodes"]?[nodeId]?["document"] != null)
        {
            return root["nodes"][nodeId]["document"] as JObject;
        }

        // Try direct document structure
        if (root["document"] != null)
        {
            return root["document"] as JObject;
        }

        return null;
    }

    public List<string> GetAllNodeIds()
    {
        var ids = new List<string>();
        foreach (var nodeData in nodeDataList)
        {
            if (!string.IsNullOrEmpty(nodeData.nodeId))
                ids.Add(nodeData.nodeId);
        }
        return ids;
    }

    public Dictionary<string, string> GetAllNodeIdsAndNames()
    {
        var dict = new Dictionary<string, string>();
        foreach (var nodeData in nodeDataList)
        {
            if (!string.IsNullOrEmpty(nodeData.nodeId))
            {
                string name = string.IsNullOrEmpty(nodeData.nodeName) ? "Unnamed" : nodeData.nodeName;
                dict[nodeData.nodeId] = name;
            }
        }
        return dict;
    }

    public bool HasNodeData(string nodeId)
    {
        return GetNodeData(nodeId) != null;
    }

    private void UpdateStats()
    {
        totalNodes = nodeDataList.Count;
        validNodes = 0;

        foreach (var nodeData in nodeDataList)
        {
            if (nodeData.isValid)
                validNodes++;
        }
    }

    [ContextMenu("Add New Node Data")]
    public void AddNewNodeData()
    {
        nodeDataList.Add(new FigmaNodeData());

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Clear All Node Data")]
    public void ClearAllNodeData()
    {
        nodeDataList.Clear();
        parsedNodeCache.Clear();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Refresh All Node Data")]
    public void RefreshAllNodeData()
    {
        ValidateAndUpdateNodes();
        UpdateStats();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Import from Resources Folder")]
    public void ImportFromResourcesFolder()
    {
#if UNITY_EDITOR
        string resourcesPath = Path.Combine(Application.dataPath, "Resources", "FigmaData");

        if (!Directory.Exists(resourcesPath))
        {
            Debug.LogWarning("Resources/FigmaData folder not found!");
            return;
        }

        var jsonFiles = Directory.GetFiles(resourcesPath, "*.json");
        int imported = 0;

        foreach (string filePath in jsonFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Extract node ID from filename (e.g., "figma_node_119-441" -> "119:441")
            if (fileName.StartsWith("figma_node_"))
            {
                string nodeId = fileName.Substring("figma_node_".Length).Replace("-", ":");

                // Check if we already have this node
                if (!HasNodeData(nodeId))
                {
                    // Load as TextAsset
                    string relativePath = "Assets/Resources/FigmaData/" + Path.GetFileName(filePath);
                    TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);

                    if (textAsset != null)
                    {
                        var newNodeData = new FigmaNodeData
                        {
                            nodeId = nodeId,
                            jsonData = textAsset,
                            description = $"Imported from {fileName}.json"
                        };

                        nodeDataList.Add(newNodeData);
                        imported++;
                    }
                }
            }
        }

        if (imported > 0)
        {
            ValidateAndUpdateNodes();
            EditorUtility.SetDirty(this);
            Debug.Log($"Imported {imported} node data files from Resources folder.");
        }
        else
        {
            Debug.Log("No new node data files found to import.");
        }
#endif
    }

    [ContextMenu("Debug Node Info")]
    public void DebugNodeInfo()
    {
        Debug.Log($"=== FigmaNodeDataAsset Debug Info ===");
        Debug.Log($"Total Nodes: {totalNodes}");
        Debug.Log($"Valid Nodes: {validNodes}");
        Debug.Log($"Cached Nodes: {parsedNodeCache.Count}");

        foreach (var nodeData in nodeDataList)
        {
            string status = nodeData.isValid ? "✅" : "❌";
            Debug.Log($"{status} {nodeData.nodeId}: {nodeData.nodeName} ({nodeData.nodeType})");
        }

        Debug.Log($"=== End Debug Info ===");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FigmaNodeDataAsset))]
public class FigmaNodeDataAssetEditor : Editor
{
    private SerializedProperty nodeDataListProp;
    private SerializedProperty autoParseOnValidateProp;
    private SerializedProperty showDebugInfoProp;

    private void OnEnable()
    {
        nodeDataListProp = serializedObject.FindProperty("nodeDataList");
        autoParseOnValidateProp = serializedObject.FindProperty("autoParseOnValidate");
        showDebugInfoProp = serializedObject.FindProperty("showDebugInfo");
    }

    public override void OnInspectorGUI()
    {
        var asset = (FigmaNodeDataAsset)target;

        serializedObject.Update();

        EditorGUILayout.LabelField("Figma Node Data Asset", EditorStyles.largeLabel);
        EditorGUILayout.Space();

        // Settings
        EditorGUILayout.PropertyField(autoParseOnValidateProp);
        EditorGUILayout.PropertyField(showDebugInfoProp);
        EditorGUILayout.Space();

        // Stats
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.IntField("Total Nodes", asset.nodeDataList.Count);
        EditorGUILayout.IntField("Valid Nodes", asset.nodeDataList.FindAll(n => n.isValid).Count);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();

        // Action buttons
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Node Data"))
        {
            asset.AddNewNodeData();
        }

        if (GUILayout.Button("Refresh All"))
        {
            asset.RefreshAllNodeData();
        }

        if (GUILayout.Button("Import from Resources"))
        {
            asset.ImportFromResourcesFolder();
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Debug Info"))
        {
            asset.DebugNodeInfo();
        }

        EditorGUILayout.Space();

        // Node list
        EditorGUILayout.PropertyField(nodeDataListProp, new GUIContent("Node Data List"), true);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
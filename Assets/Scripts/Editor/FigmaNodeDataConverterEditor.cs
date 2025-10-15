using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using TMPro;

[CustomEditor(typeof(FigmaNodeDataConverter))]
public class FigmaNodeDataConverterEditor : Editor
{
    private FigmaNodeDataConverter converter;
    private List<string> nodeIds = new List<string>();
    private List<string> nodeNames = new List<string>();
    private int selectedIndex = 0;

    private void OnEnable()
    {
        converter = (FigmaNodeDataConverter)target;
        RefreshNodeList();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Figma Node Selection", EditorStyles.boldLabel);

        if (converter.nodeDataAsset != null)
        {
            RefreshNodeList();
            selectedIndex = Mathf.Max(0, nodeIds.IndexOf(converter.targetNodeId));
            selectedIndex = EditorGUILayout.Popup("Target Node", selectedIndex, nodeNames.ToArray());
            if (selectedIndex >= 0 && selectedIndex < nodeIds.Count)
            {
                string newId = nodeIds[selectedIndex];
                if (converter.targetNodeId != newId)
                {
                    Undo.RecordObject(converter, "Change Target Node Id");
                    converter.targetNodeId = newId;
                    EditorUtility.SetDirty(converter);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a FigmaNodeDataAsset to enable node selection.", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        if (GUILayout.Button("Convert Node to UI"))
        {
            converter.ConvertNodeToUI();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Validate Setup"))
        {
            converter.ValidateSetup();
        }

        if (GUILayout.Button("Clear Created UI"))
        {
            converter.ClearCreatedUI();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("List Available Nodes"))
        {
            converter.ListAvailableNodes();
        }

        if (GUILayout.Button("Generate Prefab"))
        {
            converter.GeneratePrefab();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void RefreshNodeList()
    {
        nodeIds.Clear();
        nodeNames.Clear();
        if (converter.nodeDataAsset != null)
        {
            var allNodes = converter.nodeDataAsset.GetAllNodeIdsAndNames();
            foreach (var pair in allNodes)
            {
                nodeIds.Add(pair.Key);
                nodeNames.Add($"{pair.Value} ({pair.Key})");
            }
        }
    }
}

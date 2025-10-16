using System;
using System.Collections.Generic;
using UnityEngine;

public class FigmaSerializationTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField]
    private bool runTestsOnStart = true;

    [SerializeField]
    private bool logDetailedResults = true;

    void Start()
    {
        if (runTestsOnStart)
        {
            TestAllNodeTypes();
        }
    }

    [ContextMenu("Test All Node Types")]
    public void TestAllNodeTypes()
    {
        Debug.Log("=== Starting Figma Node Serialization Tests ===");

        var results = new List<TestResult>();

        // Test each node type
        results.Add(TestNodeType<FigmaDocumentNode>("DOCUMENT"));
        results.Add(TestNodeType<FigmaCanvasNode>("CANVAS"));
        results.Add(TestNodeType<FigmaFrameNode>("FRAME"));
        results.Add(TestNodeType<FigmaGroupNode>("GROUP"));
        results.Add(TestNodeType<FigmaComponentNode>("COMPONENT"));
        results.Add(TestNodeType<FigmaInstanceNode>("INSTANCE"));
        results.Add(TestNodeType<FigmaComponentSetNode>("COMPONENT_SET"));
        results.Add(TestNodeType<FigmaRectangleNode>("RECTANGLE"));
        results.Add(TestNodeType<FigmaTextNode>("TEXT"));
        results.Add(TestNodeType<FigmaEllipseNode>("ELLIPSE"));
        results.Add(TestNodeType<FigmaLineNode>("LINE"));
        results.Add(TestNodeType<FigmaVectorNode>("VECTOR"));
        results.Add(TestNodeType<FigmaStarNode>("STAR"));
        results.Add(TestNodeType<FigmaPolygonNode>("POLYGON"));
        results.Add(TestNodeType<FigmaBooleanOperationNode>("BOOLEAN_OPERATION"));
        results.Add(TestNodeType<FigmaSliceNode>("SLICE"));
        results.Add(TestNodeType<FigmaSectionNode>("SECTION"));
        results.Add(TestNodeType<FigmaWidgetNode>("WIDGET"));
        results.Add(TestNodeType<FigmaEmbedNode>("EMBED"));
        results.Add(TestNodeType<FigmaLinkUnfurlNode>("LINK_UNFURL"));
        results.Add(TestNodeType<FigmaMediaNode>("MEDIA"));
        results.Add(TestNodeType<FigmaTableNode>("TABLE"));
        results.Add(TestNodeType<FigmaTableCellNode>("TABLE_CELL"));
        results.Add(TestNodeType<FigmaStickyNode>("STICKY"));
        results.Add(TestNodeType<FigmaShapeWithTextNode>("SHAPE_WITH_TEXT"));
        results.Add(TestNodeType<FigmaConnectorNode>("CONNECTOR"));
        results.Add(TestNodeType<FigmaWashiTapeNode>("WASHI_TAPE"));
        results.Add(TestNodeType<FigmaCodeBlockNode>("CODE_BLOCK"));
        results.Add(TestNodeType<FigmaStampNode>("STAMP"));

        // Summary
        int passed = 0;
        int failed = 0;

        foreach (var result in results)
        {
            if (result.success)
                passed++;
            else
                failed++;

            if (logDetailedResults || !result.success)
            {
                Debug.Log($"{(result.success ? "✓" : "✗")} {result.nodeType}: {result.message}");
            }
        }

        Debug.Log($"=== Test Results: {passed} passed, {failed} failed ===");

        if (failed == 0)
        {
            Debug.Log("🎉 All Figma node types are properly serializable!");
        }
        else
        {
            Debug.LogWarning(
                $"⚠️ {failed} node type(s) have serialization issues that need to be addressed."
            );
        }
    }

    private TestResult TestNodeType<T>(string nodeTypeName)
        where T : FigmaNode, new()
    {
        try
        {
            // Create test instance
            T testNode = CreateTestNode<T>();

            // Test serialization
            string json = FigmaSerializationUtility.SerializeNode(testNode);
            if (string.IsNullOrEmpty(json))
            {
                return new TestResult(nodeTypeName, false, "Serialization returned null or empty");
            }

            // Test deserialization
            FigmaNode deserializedNode = FigmaSerializationUtility.DeserializeNode(json);
            if (deserializedNode == null)
            {
                return new TestResult(nodeTypeName, false, "Deserialization returned null");
            }

            // Verify type matches
            if (deserializedNode.GetType() != typeof(T))
            {
                return new TestResult(
                    nodeTypeName,
                    false,
                    $"Type mismatch: expected {typeof(T).Name}, got {deserializedNode.GetType().Name}"
                );
            }

            return new TestResult(nodeTypeName, true, "Serialization successful");
        }
        catch (Exception ex)
        {
            return new TestResult(nodeTypeName, false, $"Exception: {ex.Message}");
        }
    }

    private T CreateTestNode<T>()
        where T : FigmaNode, new()
    {
        T node = new T();

        // Set common properties
        node.id = System.Guid.NewGuid().ToString();
        node.name = $"Test {typeof(T).Name}";
        node.visible = true;
        node.locked = false;

        // Set type-specific properties
        SetTypeSpecificProperties(node);

        return node;
    }

    private void SetTypeSpecificProperties(FigmaNode node)
    {
        switch (node)
        {
            case FigmaTextNode textNode:
                textNode.characters = "Test Text";
                textNode.fontSize = 16f;
                break;

            case FigmaFrameNode frameNode:
                frameNode.backgroundColor = new FigmaColor(1f, 1f, 1f, 1f);
                frameNode.cornerRadius = 8f;
                break;

            case FigmaRectangleNode rectangleNode:
                rectangleNode.cornerRadius = 4f;
                break;

            case FigmaEllipseNode ellipseNode:
                ellipseNode.arcData = new float[] { 0f, 360f, 0f };
                break;

            case FigmaStarNode starNode:
                starNode.count = 5;
                starNode.radius = 50f;
                starNode.innerRadius = 25f;
                break;

            case FigmaPolygonNode polygonNode:
                polygonNode.count = 6;
                break;

            case FigmaComponentNode componentNode:
                componentNode.key = "test-component-key";
                componentNode.description = "Test component";
                break;

            case FigmaInstanceNode instanceNode:
                instanceNode.componentId = "test-component-id";
                instanceNode.componentKey = "test-component-key";
                break;

            case FigmaBooleanOperationNode booleanNode:
                booleanNode.booleanOperation = FigmaBooleanOperationType.UNION;
                break;

            case FigmaTableNode tableNode:
                tableNode.rows = 3;
                tableNode.columns = 4;
                break;

            case FigmaCodeBlockNode codeBlockNode:
                codeBlockNode.code = "console.log('Hello World');";
                codeBlockNode.language = "javascript";
                break;

            case FigmaConnectorNode connectorNode:
                connectorNode.connectorStart = "node-1";
                connectorNode.connectorEnd = "node-2";
                break;
        }
    }

    private struct TestResult
    {
        public string nodeType;
        public bool success;
        public string message;

        public TestResult(string nodeType, bool success, string message)
        {
            this.nodeType = nodeType;
            this.success = success;
            this.message = message;
        }
    }
}

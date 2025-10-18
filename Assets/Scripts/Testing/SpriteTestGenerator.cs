using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quick test script for generating sprites from JSON input
/// Useful for testing sprite generation without full Figma conversion
/// </summary>
public class SpriteTestGenerator : MonoBehaviour
{
    [Header("Test Configuration")]
    [TextArea(10, 20)]
    public string jsonInput =
        @"{
        ""id"": ""test-node"",
        ""name"": ""TestRectangle"",
        ""type"": ""RECTANGLE"",
        ""absoluteBoundingBox"": {
            ""x"": 0,
            ""y"": 0,
            ""width"": 200,
            ""height"": 100
        },
        ""fills"": [
            {
                ""type"": ""SOLID"",
                ""color"": {
                    ""r"": 0.2,
                    ""g"": 0.6,
                    ""b"": 1.0
                },
                ""opacity"": 1.0
            }
        ],
        ""cornerRadius"": 10
    }";

    [Header("Sprite Settings")]
    public float width = 200f;
    public float height = 100f;
    public bool saveToResources = true;
    public string spriteName = "TestSprite";

    [Header("Debug")]
    public bool enableDebugLogs = true;

    [ContextMenu("Generate Sprite from JSON")]
    public void GenerateSpriteFromJson()
    {
        try
        {
            if (string.IsNullOrEmpty(jsonInput))
            {
                Debug.LogError("JSON input is empty!");
                return;
            }

            // Parse JSON
            JObject nodeData = JObject.Parse(jsonInput);

            if (enableDebugLogs)
            {
                Debug.Log("✓ Parsed JSON successfully");
                Debug.Log($"Node: {nodeData["name"]} (Type: {nodeData["type"]})");
            }

            // Extract dimensions from JSON if available
            float actualWidth = width;
            float actualHeight = height;

            JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
            if (boundingBox != null)
            {
                actualWidth = boundingBox["width"]?.ToObject<float>() ?? width;
                actualHeight = boundingBox["height"]?.ToObject<float>() ?? height;

                if (enableDebugLogs)
                {
                    Debug.Log($"✓ Extracted dimensions from JSON: {actualWidth}x{actualHeight}");
                }
            }

            // Generate sprite
            Sprite sprite = SpriteGenerator.GenerateSpriteFromNodeDirect(
                nodeData,
                actualWidth,
                actualHeight
            );

            if (sprite == null)
            {
                Debug.LogError("Failed to generate sprite!");
                return;
            }

            if (enableDebugLogs)
                Debug.Log(
                    $"✓ Generated sprite: {sprite.name} ({sprite.texture.width}x{sprite.texture.height})"
                );

            // Save to Resources if enabled
            if (saveToResources)
            {
                string savedPath = SpriteSaveUtility.SaveSpriteToResources(
                    sprite,
                    spriteName,
                    "test-node"
                );

                if (!string.IsNullOrEmpty(savedPath))
                {
                    Debug.Log($"✓ Saved sprite to: {savedPath}");

                    // Load the saved sprite to verify
                    Sprite savedSprite = Resources.Load<Sprite>(savedPath);
                    if (savedSprite != null)
                    {
                        Debug.Log($"✓ Verified saved sprite loaded successfully");
                    }
                }
                else
                {
                    Debug.LogWarning("Failed to save sprite to Resources");
                }
            }

            // Create a test GameObject to display the sprite
            CreateTestGameObject(sprite, actualWidth, actualHeight);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating sprite: {ex.Message}");
            if (enableDebugLogs)
                Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private void CreateTestGameObject(Sprite sprite, float spriteWidth, float spriteHeight)
    {
        // Create a test canvas if none exists
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("TestCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create test GameObject with the sprite
        GameObject testGO = new GameObject($"Test_{spriteName}");
        testGO.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = testGO.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(spriteWidth, spriteHeight);
        rectTransform.anchoredPosition = Vector2.zero;

        Image image = testGO.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        if (enableDebugLogs)
            Debug.Log($"✓ Created test GameObject: {testGO.name} ({spriteWidth}x{spriteHeight})");
    }

    private void OnValidate()
    {
        // Auto-update sprite name from JSON if possible
        if (!string.IsNullOrEmpty(jsonInput))
        {
            try
            {
                JObject nodeData = JObject.Parse(jsonInput);
                string name = nodeData["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    spriteName = name.SanitizeFileName();
                }
            }
            catch
            {
                // Ignore JSON parsing errors during validation
            }
        }
    }
}

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
            Sprite sprite = DirectSpriteGenerator.GenerateSpriteFromNodeDirect(
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

    [ContextMenu("Test Rectangle Sprite")]
    public void TestRectangleSprite()
    {
        jsonInput =
            @"{
            ""id"": ""test-rect"",
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

        width = 200f;
        height = 100f;
        spriteName = "TestRectangle";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Ellipse Sprite")]
    public void TestEllipseSprite()
    {
        jsonInput =
            @"{
            ""id"": ""test-ellipse"",
            ""name"": ""TestEllipse"",
            ""type"": ""ELLIPSE"",
            ""absoluteBoundingBox"": {
                ""x"": 0,
                ""y"": 0,
                ""width"": 150,
                ""height"": 150
            },
            ""fills"": [
                {
                    ""type"": ""SOLID"",
                    ""color"": {
                        ""r"": 1.0,
                        ""g"": 0.3,
                        ""b"": 0.3
                    },
                    ""opacity"": 1.0
                }
            ]
        }";

        width = 150f;
        height = 150f;
        spriteName = "TestEllipse";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Frame Sprite")]
    public void TestFrameSprite()
    {
        jsonInput =
            @"{
            ""id"": ""test-frame"",
            ""name"": ""TestFrame"",
            ""type"": ""FRAME"",
            ""absoluteBoundingBox"": {
                ""x"": 0,
                ""y"": 0,
                ""width"": 300,
                ""height"": 200
            },
            ""fills"": [
                {
                    ""type"": ""SOLID"",
                    ""color"": {
                        ""r"": 0.1,
                        ""g"": 0.8,
                        ""b"": 0.1
                    },
                    ""opacity"": 0.8
                }
            ],
            ""cornerRadius"": 20
        }";

        width = 300f;
        height = 200f;
        spriteName = "TestFrame";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Gradient Sprite")]
    public void TestGradientSprite()
    {
        jsonInput =
            @"{
            ""id"": ""test-gradient"",
            ""name"": ""TestGradient"",
            ""type"": ""RECTANGLE"",
            ""absoluteBoundingBox"": {
                ""x"": 0,
                ""y"": 0,
                ""width"": 250,
                ""height"": 150
            },
            ""fills"": [
                {
                    ""type"": ""GRADIENT_LINEAR"",
                    ""gradientStops"": [
                        {
                            ""color"": {
                                ""r"": 1.0,
                                ""g"": 0.0,
                                ""b"": 0.0
                            },
                            ""position"": 0.0
                        },
                        {
                            ""color"": {
                                ""r"": 0.0,
                                ""g"": 0.0,
                                ""b"": 1.0
                            },
                            ""position"": 1.0
                        }
                    ]
                }
            ],
            ""cornerRadius"": 15
        }";

        width = 250f;
        height = 150f;
        spriteName = "TestGradient";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Drop Shadow Sprite")]
    public void TestDropShadowSprite()
    {
        jsonInput = @"{
            ""id"": ""test-shadow"",
            ""name"": ""TestDropShadow"",
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
            ""cornerRadius"": 10,
            ""effects"": [
                {
                    ""type"": ""DROP_SHADOW"",
                    ""visible"": true,
                    ""radius"": 8,
                    ""color"": {
                        ""r"": 0.0,
                        ""g"": 0.0,
                        ""b"": 0.0,
                        ""a"": 0.3
                    },
                    ""offset"": {
                        ""x"": 4,
                        ""y"": 4
                    }
                }
            ]
        }";
        
        width = 200f;
        height = 100f;
        spriteName = "TestDropShadow";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Inner Shadow Sprite")]
    public void TestInnerShadowSprite()
    {
        jsonInput = @"{
            ""id"": ""test-inner-shadow"",
            ""name"": ""TestInnerShadow"",
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
                        ""r"": 0.8,
                        ""g"": 0.2,
                        ""b"": 0.2
                    },
                    ""opacity"": 1.0
                }
            ],
            ""cornerRadius"": 10,
            ""effects"": [
                {
                    ""type"": ""INNER_SHADOW"",
                    ""visible"": true,
                    ""radius"": 6,
                    ""color"": {
                        ""r"": 0.0,
                        ""g"": 0.0,
                        ""b"": 0.0,
                        ""a"": 0.4
                    },
                    ""offset"": {
                        ""x"": 2,
                        ""y"": 2
                    }
                }
            ]
        }";
        
        width = 200f;
        height = 100f;
        spriteName = "TestInnerShadow";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Test Real Figma Data")]
    public void TestRealFigmaData()
    {
        jsonInput = @"{
            ""id"": ""1041:176"",
            ""name"": ""Frame 403"",
            ""type"": ""FRAME"",
            ""blendMode"": ""PASS_THROUGH"",
            ""fills"": [
                {
                    ""blendMode"": ""NORMAL"",
                    ""type"": ""SOLID"",
                    ""color"": {
                        ""r"": 0.4,
                        ""g"": 0.4,
                        ""b"": 0.4,
                        ""a"": 1.0
                    }
                }
            ],
            ""strokes"": [
                {
                    ""blendMode"": ""NORMAL"",
                    ""type"": ""SOLID"",
                    ""color"": { ""r"": 0.0, ""g"": 0.0, ""b"": 0.0, ""a"": 1.0 }
                }
            ],
            ""strokeWeight"": 3.0,
            ""strokeAlign"": ""INSIDE"",
            ""absoluteBoundingBox"": {
                ""x"": 1914.5,
                ""y"": 394.0,
                ""width"": 220.0,
                ""height"": 64.0
            },
            ""effects"": [
                {
                    ""type"": ""DROP_SHADOW"",
                    ""visible"": true,
                    ""color"": {
                        ""r"": 0.00076998199801892042,
                        ""g"": 0.00076998199801892042,
                        ""b"": 0.00076998199801892042,
                        ""a"": 1.0
                    },
                    ""blendMode"": ""NORMAL"",
                    ""offset"": { ""x"": -3.0, ""y"": 3.0 },
                    ""radius"": 0.0,
                    ""showShadowBehindNode"": false
                }
            ]
        }";
        
        // Don't set width/height - let it extract from JSON
        spriteName = "Frame403";
        GenerateSpriteFromJson();
    }

    [ContextMenu("Clear Test Objects")]
    public void ClearTestObjects()
    {
        // Find and destroy test objects
        GameObject[] testObjects = GameObject.FindGameObjectsWithTag("Untagged");
        int destroyed = 0;

        foreach (GameObject obj in testObjects)
        {
            if (obj.name.StartsWith("Test_") || obj.name == "TestCanvas")
            {
                DestroyImmediate(obj);
                destroyed++;
            }
        }

        if (enableDebugLogs)
            Debug.Log($"✓ Cleared {destroyed} test objects");
    }

    [ContextMenu("Show Saved Sprites")]
    public void ShowSavedSprites()
    {
        string spriteFolderPath = Path.Combine(
            Application.dataPath,
            "Resources",
            "Sprites",
            "test-node"
        );

        if (Directory.Exists(spriteFolderPath))
        {
            string[] files = Directory.GetFiles(spriteFolderPath, "*.png");
            Debug.Log($"✓ Test sprites folder: Assets/Resources/Sprites/test-node");
            Debug.Log($"✓ Total test sprites: {files.Length}");

            if (files.Length > 0)
            {
                Debug.Log("Saved test sprites:");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    Debug.Log($"  - {fileName}");
                }
            }
        }
        else
        {
            Debug.LogWarning("No test sprites saved yet");
        }
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

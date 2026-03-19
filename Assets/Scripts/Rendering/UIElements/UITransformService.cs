using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Handles transform and layout calculations for UI elements
/// </summary>
public class UITransformService
{
    private readonly FigmaConverterConfig _config;
    private readonly Dictionary<GameObject, Vector2> _figmaPositions;

    public UITransformService(FigmaConverterConfig config)
    {
        _config = config;
        _figmaPositions = new Dictionary<GameObject, Vector2>();
    }

    /// <summary>
    /// Applies Figma transform data to Unity RectTransform
    /// </summary>
    public void ApplyTransform(JObject nodeData, GameObject gameObject, Canvas targetCanvas)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        JObject boundingBox = nodeData["absoluteBoundingBox"] as JObject;
        if (boundingBox == null)
            return;

        float x = boundingBox["x"]?.ToObject<float>() ?? 0f;
        float y = boundingBox["y"]?.ToObject<float>() ?? 0f;
        float width = boundingBox["width"]?.ToObject<float>() ?? 100f;
        float height = boundingBox["height"]?.ToObject<float>() ?? 100f;

        // Store original Figma position for relative calculations
        _figmaPositions[gameObject] = new Vector2(x, y);

        // Apply scale
        x *= _config.scaleFactor;
        y *= _config.scaleFactor;
        width *= _config.scaleFactor;
        height *= _config.scaleFactor;

        // Set size
        rectTransform.sizeDelta = new Vector2(width, height);

        // Calculate and set position
        Vector2 relativePosition = CalculateRelativePosition(
            rectTransform,
            x,
            y,
            width,
            height,
            targetCanvas
        );
        rectTransform.anchoredPosition = relativePosition;

        // Set anchors and pivot
        SetAnchorsAndPivot(rectTransform, targetCanvas);
    }

    /// <summary>
    /// Applies visibility from Figma node
    /// </summary>
    public void ApplyVisibility(JObject nodeData, GameObject gameObject)
    {
        bool visible = nodeData["visible"]?.ToObject<bool>() ?? true;
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Clears cached positions
    /// </summary>
    public void ClearCache()
    {
        _figmaPositions.Clear();
    }

    private Vector2 CalculateRelativePosition(
        RectTransform rectTransform,
        float x,
        float y,
        float width,
        float height,
        Canvas targetCanvas
    )
    {
        if (rectTransform.parent == targetCanvas.transform)
        {
            // Root level - position relative to canvas center (pivot = 0.5,0.5)
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Figma top-left → Unity center-pivot: offset by half element size
            float unityX = x - (canvasWidth * 0.5f) + (width * 0.5f);
            float unityY = (canvasHeight * 0.5f) - y - (height * 0.5f);

            return new Vector2(unityX, unityY);
        }
        else
        {
            // Child element - position relative to parent
            GameObject parentGameObject = rectTransform.parent.gameObject;

            if (_figmaPositions.TryGetValue(parentGameObject, out Vector2 parentFigmaPos))
            {
                float relativeX = (x / _config.scaleFactor) - parentFigmaPos.x;
                float relativeY = (y / _config.scaleFactor) - parentFigmaPos.y;

                relativeX *= _config.scaleFactor;
                relativeY *= _config.scaleFactor;

                // Pivot = (0.5, 0.5): offset anchoredPosition by half element size
                return new Vector2(relativeX + width * 0.5f, -relativeY - height * 0.5f);
            }
            else
            {
                // Fallback to absolute positioning with center pivot offset
                return new Vector2(x + width * 0.5f, -y - height * 0.5f);
            }
        }
    }

    private void SetAnchorsAndPivot(RectTransform rectTransform, Canvas targetCanvas)
    {
        if (rectTransform.parent == targetCanvas.transform)
        {
            // Root level - centered anchors, center pivot
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            // Child element - top-left anchors, center pivot
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}

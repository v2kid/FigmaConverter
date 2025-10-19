using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Generates shape masks for different node types
/// Handles rectangle, ellipse, and rounded rectangle mask generation
/// </summary>
public static class ShapeMaskGenerator
{
    /// <summary>
    /// Creates a mask for the specified node type
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the mask</param>
    /// <param name="height">Height of the mask</param>
    /// <returns>Boolean array representing the mask</returns>
    public static bool[] CreateMask(JObject nodeData, int width, int height)
    {
        if (nodeData == null)
        {
            Debug.LogError("ShapeMaskGenerator: Cannot create mask for null node data");
            return new bool[width * height];
        }

        string nodeType = nodeData["type"]?.ToString()?.ToUpper();

        switch (nodeType)
        {
            case "RECTANGLE":
            case "ROUNDED_RECTANGLE":
            case "FRAME":
                return CreateRectangleMask(nodeData, width, height);

            case "ELLIPSE":
                return CreateEllipseMask(nodeData, width, height);

            default:
                // Default to rectangle for unknown types
                return CreateRectangleMask(nodeData, width, height);
        }
    }

    /// <summary>
    /// Creates a rectangular mask with optional rounded corners
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the mask</param>
    /// <param name="height">Height of the mask</param>
    /// <returns>Boolean array representing the rectangular mask</returns>
    public static bool[] CreateRectangleMask(JObject nodeData, int width, int height)
    {
        bool[] mask = new bool[width * height];
        CreateRectangleMask(nodeData, mask, width, height);
        return mask;
    }

    /// <summary>
    /// Fills an existing mask array with rectangular mask data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="mask">Mask array to fill</param>
    /// <param name="width">Width of the mask</param>
    /// <param name="height">Height of the mask</param>
    public static void CreateRectangleMask(JObject nodeData, bool[] mask, int width, int height)
    {
        if (mask == null || mask.Length != width * height)
        {
            Debug.LogError("ShapeMaskGenerator: Invalid mask array for rectangle mask");
            return;
        }

        // Get corner radius from node data
        float cornerRadius = GetCornerRadius(nodeData);

        // Fill the mask
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                bool insideShape = true; // Default to true like DirectSpriteGenerator

                if (cornerRadius > 0)
                {
                    // Check if point is inside rounded rectangle
                    insideShape = IsInsideRoundedRect(x, y, width, height, cornerRadius);
                }
                // For simple rectangle, all points inside bounds are valid (insideShape = true)

                mask[index] = insideShape;
            }
        }
    }

    /// <summary>
    /// Creates an elliptical mask
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="width">Width of the mask</param>
    /// <param name="height">Height of the mask</param>
    /// <returns>Boolean array representing the elliptical mask</returns>
    public static bool[] CreateEllipseMask(JObject nodeData, int width, int height)
    {
        bool[] mask = new bool[width * height];
        CreateEllipseMask(nodeData, mask, width, height);
        return mask;
    }

    /// <summary>
    /// Fills an existing mask array with elliptical mask data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="mask">Mask array to fill</param>
    /// <param name="width">Width of the mask</param>
    /// <param name="height">Height of the mask</param>
    public static void CreateEllipseMask(JObject nodeData, bool[] mask, int width, int height)
    {
        if (mask == null || mask.Length != width * height)
        {
            Debug.LogError("ShapeMaskGenerator: Invalid mask array for ellipse mask");
            return;
        }

        // Calculate ellipse center and radii
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;

        // Fill the mask
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;

                // Check if point is inside ellipse using ellipse equation
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distance = dx * dx + dy * dy;

                mask[index] = distance <= 1.0f;
            }
        }
    }

    /// <summary>
    /// Checks if a point is inside a rounded rectangle
    /// </summary>
    /// <param name="x">X coordinate of the point</param>
    /// <param name="y">Y coordinate of the point</param>
    /// <param name="width">Width of the rectangle</param>
    /// <param name="height">Height of the rectangle</param>
    /// <param name="radius">Corner radius</param>
    /// <returns>True if point is inside the rounded rectangle</returns>
    public static bool IsInsideRoundedRect(int x, int y, int width, int height, float radius)
    {
        // Clamp radius to half the smallest dimension
        radius = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);

        // Check if point is in the center rectangle (no rounding needed)
        if (x >= radius && x < width - radius && y >= radius && y < height - radius)
        {
            return true;
        }

        // Check corners
        if (x < radius && y < radius)
        {
            // Top-left corner
            float dx = x - radius;
            float dy = y - radius;
            return dx * dx + dy * dy <= radius * radius;
        }
        else if (x >= width - radius && y < radius)
        {
            // Top-right corner
            float dx = x - (width - radius);
            float dy = y - radius;
            return dx * dx + dy * dy <= radius * radius;
        }
        else if (x < radius && y >= height - radius)
        {
            // Bottom-left corner
            float dx = x - radius;
            float dy = y - (height - radius);
            return dx * dx + dy * dy <= radius * radius;
        }
        else if (x >= width - radius && y >= height - radius)
        {
            // Bottom-right corner
            float dx = x - (width - radius);
            float dy = y - (height - radius);
            return dx * dx + dy * dy <= radius * radius;
        }

        // Check edges (rectangular parts)
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    /// <summary>
    /// Gets the corner radius from node data
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <returns>Corner radius value</returns>
    private static float GetCornerRadius(JObject nodeData)
    {
        if (nodeData == null)
            return SpriteGenerationConstants.DEFAULT_CORNER_RADIUS;

        // Try to get corner radius from various possible locations
        var cornerRadius = nodeData["cornerRadius"];
        if (cornerRadius != null)
        {
            return cornerRadius.Value<float>();
        }

        // Try rectangle corner radii
        var rectangleCornerRadii = nodeData["rectangleCornerRadii"];
        if (rectangleCornerRadii != null)
        {
            // Use the first corner radius if available
            var radii = rectangleCornerRadii as JArray;
            if (radii != null && radii.Count > 0)
            {
                return radii[0].Value<float>();
            }
        }

        return SpriteGenerationConstants.DEFAULT_CORNER_RADIUS;
    }

    /// <summary>
    /// Creates a shadow mask for drop shadow effects
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="nodeWidth">Width of the node</param>
    /// <param name="nodeHeight">Height of the node</param>
    /// <returns>Boolean array representing the shadow mask</returns>
    public static bool[] CreateShadowMask(JObject nodeData, float nodeWidth, float nodeHeight)
    {
        // For now, use the same mask as the main shape
        // In the future, this could be enhanced to create a blurred version
        return CreateMask(nodeData, (int)nodeWidth, (int)nodeHeight);
    }

    /// <summary>
    /// Inverts a mask (true becomes false and vice versa)
    /// </summary>
    /// <param name="mask">Mask to invert</param>
    /// <returns>Inverted mask</returns>
    public static bool[] InvertMask(bool[] mask)
    {
        if (mask == null)
            return null;

        bool[] invertedMask = new bool[mask.Length];
        for (int i = 0; i < mask.Length; i++)
        {
            invertedMask[i] = !mask[i];
        }

        return invertedMask;
    }

    /// <summary>
    /// Combines two masks using AND operation
    /// </summary>
    /// <param name="mask1">First mask</param>
    /// <param name="mask2">Second mask</param>
    /// <returns>Combined mask</returns>
    public static bool[] CombineMasksAnd(bool[] mask1, bool[] mask2)
    {
        if (mask1 == null || mask2 == null || mask1.Length != mask2.Length)
        {
            Debug.LogError("ShapeMaskGenerator: Cannot combine masks with different lengths");
            return mask1 ?? mask2;
        }

        bool[] combinedMask = new bool[mask1.Length];
        for (int i = 0; i < mask1.Length; i++)
        {
            combinedMask[i] = mask1[i] && mask2[i];
        }

        return combinedMask;
    }

    /// <summary>
    /// Combines two masks using OR operation
    /// </summary>
    /// <param name="mask1">First mask</param>
    /// <param name="mask2">Second mask</param>
    /// <returns>Combined mask</returns>
    public static bool[] CombineMasksOr(bool[] mask1, bool[] mask2)
    {
        if (mask1 == null || mask2 == null || mask1.Length != mask2.Length)
        {
            Debug.LogError("ShapeMaskGenerator: Cannot combine masks with different lengths");
            return mask1 ?? mask2;
        }

        bool[] combinedMask = new bool[mask1.Length];
        for (int i = 0; i < mask1.Length; i++)
        {
            combinedMask[i] = mask1[i] || mask2[i];
        }

        return combinedMask;
    }
}

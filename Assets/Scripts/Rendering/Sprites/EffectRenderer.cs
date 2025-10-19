using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Handles rendering of effects like drop shadows, glows, and other visual effects
/// Calculates effect padding and renders effects to pixel arrays
/// </summary>
public static class EffectRenderer
{
    /// <summary>
    /// Calculates directional padding needed for effects like drop shadows
    /// Returns Vector4(left, right, top, bottom)
    /// </summary>
    /// <param name="nodeData">Figma node data containing effects</param>
    /// <returns>Padding vector (left, right, top, bottom)</returns>
    public static Vector4 CalculateEffectPaddingDirectional(JObject nodeData)
    {
        if (nodeData == null)
            return Vector4.zero;

        JArray effects = nodeData["effects"] as JArray;
        if (effects == null || effects.Count == 0)
            return Vector4.zero;

        float leftPadding = 0f;
        float rightPadding = 0f;
        float topPadding = 0f;
        float bottomPadding = 0f;

        foreach (JObject effect in effects)
        {
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
                continue;

            string effectType = effect["type"]?.ToString();
            if (
                effectType != "DROP_SHADOW"
                && effectType != "INNER_SHADOW"
                && effectType != "LAYER_BLUR"
                && effectType != "BACKGROUND_BLUR"
            )
                continue;

            float offsetX = effect["offset"]?["x"]?.Value<float>() ?? 0f;
            float offsetY = effect["offset"]?["y"]?.Value<float>() ?? 0f;
            float blurRadius = effect["radius"]?.Value<float>() ?? 0f;
            // Note: Not using spread to match DirectSpriteGenerator behavior

            switch (effectType)
            {
                case "DROP_SHADOW":
                case "INNER_SHADOW":
                    // Calculate directional padding based on offset - like DirectSpriteGenerator
                    if (offsetX < 0)
                    {
                        leftPadding = Mathf.Max(leftPadding, Mathf.Abs(offsetX) + blurRadius);
                    }
                    else
                    {
                        rightPadding = Mathf.Max(rightPadding, offsetX + blurRadius);
                    }

                    if (offsetY < 0)
                    {
                        topPadding = Mathf.Max(topPadding, Mathf.Abs(offsetY) + blurRadius);
                    }
                    else
                    {
                        bottomPadding = Mathf.Max(bottomPadding, offsetY + blurRadius);
                    }
                    break;

                case "LAYER_BLUR":
                case "BACKGROUND_BLUR":
                    // Blur lan đều ra mọi hướng
                    leftPadding =
                        rightPadding =
                        topPadding =
                        bottomPadding =
                            Mathf.Max(leftPadding, blurRadius * 2f);
                    break;
            }
        }

        return new Vector4(leftPadding, rightPadding, bottomPadding, topPadding);
    }

    /// <summary>
    /// Renders all drop shadows for a node
    /// </summary>
    /// <param name="nodeData">Figma node data</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="nodeWidth">Width of the node</param>
    /// <param name="nodeHeight">Height of the node</param>
    /// <param name="offsetX">X offset of the node in texture</param>
    /// <param name="offsetY">Y offset of the node in texture</param>
    public static void RenderDropShadows(
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY
    )
    {
        if (nodeData == null || pixels == null)
            return;

        JArray effects = nodeData["effects"] as JArray;
        if (effects == null || effects.Count == 0)
            return;

        foreach (JObject effect in effects)
        {
            bool visible = effect["visible"]?.ToObject<bool>() ?? true;
            if (!visible)
                continue;

            string effectType = effect["type"]?.ToString();
            if (effectType == "DROP_SHADOW")
            {
                RenderDropShadow(
                    effect,
                    nodeData,
                    pixels,
                    textureWidth,
                    textureHeight,
                    nodeWidth,
                    nodeHeight,
                    offsetX,
                    offsetY
                );
            }
        }
    }

    /// <summary>
    /// Renders a single drop shadow effect
    /// </summary>
    /// <param name="effectData">Effect data from Figma</param>
    /// <param name="nodeData">Original node data for mask generation</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="nodeWidth">Width of the node</param>
    /// <param name="nodeHeight">Height of the node</param>
    /// <param name="offsetX">X offset of the node in texture</param>
    /// <param name="offsetY">Y offset of the node in texture</param>
    private static void RenderDropShadow(
        JObject effectData,
        JObject nodeData,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int offsetX,
        int offsetY
    )
    {
        // Get effect properties
        float offsetXValue = effectData["offset"]?["x"]?.Value<float>() ?? 0f;
        float offsetYValue = -(effectData["offset"]?["y"]?.Value<float>() ?? 0f); // Flip Y coordinate like original
        float blurRadius = effectData["radius"]?.Value<float>() ?? 0f;
        float spread = effectData["spread"]?.Value<float>() ?? 0f;

        // Get shadow color
        Color shadowColor = GetEffectColor(effectData);

        // Create shadow mask
        bool[] shadowMask = ShapeMaskGenerator.CreateShadowMask(nodeData, nodeWidth, nodeHeight);

        // Render shadow with mask
        RenderShadowWithMask(
            shadowMask,
            pixels,
            textureWidth,
            textureHeight,
            nodeWidth,
            nodeHeight,
            offsetX + (int)offsetXValue,
            offsetY + (int)offsetYValue,
            shadowColor,
            blurRadius,
            spread
        );
    }

    /// <summary>
    /// Renders a shadow using a mask
    /// </summary>
    /// <param name="shadowMask">Mask defining the shadow shape</param>
    /// <param name="pixels">Pixel array to render to</param>
    /// <param name="textureWidth">Width of the texture</param>
    /// <param name="textureHeight">Height of the texture</param>
    /// <param name="nodeWidth">Width of the node</param>
    /// <param name="nodeHeight">Height of the node</param>
    /// <param name="shadowOffsetX">X offset of the shadow</param>
    /// <param name="shadowOffsetY">Y offset of the shadow</param>
    /// <param name="shadowColor">Color of the shadow</param>
    /// <param name="blurRadius">Blur radius for the shadow</param>
    /// <param name="spread">Spread amount for the shadow</param>
    private static void RenderShadowWithMask(
        bool[] shadowMask,
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        float nodeWidth,
        float nodeHeight,
        int shadowOffsetX,
        int shadowOffsetY,
        Color shadowColor,
        float blurRadius,
        float spread
    )
    {
        if (shadowMask == null || pixels == null)
            return;

        int maskWidth = (int)nodeWidth;
        int maskHeight = (int)nodeHeight;

        // Simple blur implementation like original
        int blurPixels = Mathf.Max(1, (int)blurRadius);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                // Check if this pixel should have shadow
                bool hasShadow = false;
                float shadowIntensity = 0f;

                // Sample the mask with blur
                for (int dy = -blurPixels; dy <= blurPixels; dy++)
                {
                    for (int dx = -blurPixels; dx <= blurPixels; dx++)
                    {
                        int maskX = x - shadowOffsetX + dx;
                        int maskY = y - shadowOffsetY + dy;

                        if (maskX >= 0 && maskX < maskWidth && maskY >= 0 && maskY < maskHeight)
                        {
                            int maskIndex = maskY * maskWidth + maskX;
                            if (maskIndex < shadowMask.Length && shadowMask[maskIndex])
                            {
                                // Calculate distance for blur falloff
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                if (distance <= blurPixels)
                                {
                                    float intensity = 1f - (distance / blurPixels);
                                    shadowIntensity = Mathf.Max(shadowIntensity, intensity);
                                    hasShadow = true;
                                }
                            }
                        }
                    }
                }

                if (hasShadow)
                {
                    int index = y * textureWidth + x;
                    Color finalShadowColor = shadowColor;
                    finalShadowColor.a *= shadowIntensity;

                    // Alpha blend with existing pixel
                    Color existing = pixels[index];
                    pixels[index] = Color.Lerp(existing, finalShadowColor, finalShadowColor.a);
                }
            }
        }
    }

    /// <summary>
    /// Gets the color from an effect
    /// </summary>
    /// <param name="effectData">Effect data</param>
    /// <returns>Effect color</returns>
    private static Color GetEffectColor(JObject effectData)
    {
        if (effectData == null)
            return Color.black;

        JObject colorData = effectData["color"] as JObject;
        if (colorData == null)
            return Color.black;

        float r = colorData["r"]?.Value<float>() ?? 0f;
        float g = colorData["g"]?.Value<float>() ?? 0f;
        float b = colorData["b"]?.Value<float>() ?? 0f;
        float a = effectData["opacity"]?.Value<float>() ?? 1f;

        return new Color(r, g, b, a);
    }
}

using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FigmaColorParser
{
    public static Color ParseColor(JObject colorData, float defaultOpacity = 1f)
    {
        if (colorData == null)
            return Color.clear;

        float r = colorData["r"]?.ToObject<float>() ?? 0f;
        float g = colorData["g"]?.ToObject<float>() ?? 0f;
        float b = colorData["b"]?.ToObject<float>() ?? 0f;
        float a = colorData["a"]?.ToObject<float>() ?? defaultOpacity;

        return new Color(r, g, b, a);
    }

    public static Color ParseColorWithOpacity(JObject fillData)
    {
        if (fillData == null)
            return Color.clear;

        JObject colorObj = fillData["color"] as JObject;
        float opacity = fillData["opacity"]?.ToObject<float>() ?? 1f;

        if (colorObj != null)
        {
            float r = colorObj["r"]?.ToObject<float>() ?? 0f;
            float g = colorObj["g"]?.ToObject<float>() ?? 0f;
            float b = colorObj["b"]?.ToObject<float>() ?? 0f;
            float a = (colorObj["a"]?.ToObject<float>() ?? 1f) * opacity;

            return new Color(r, g, b, a);
        }

        return Color.clear;
    }
}

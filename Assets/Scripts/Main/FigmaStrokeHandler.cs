using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FigmaStrokeHandler
{
    public static string GenerateSVGStroke(JObject nodeData)
    {
        JArray strokes = nodeData["strokes"] as JArray;
        if (strokes == null || strokes.Count == 0)
            return "stroke=\"none\"";

        JObject firstStroke = strokes[0] as JObject;
        if (firstStroke == null)
            return "stroke=\"none\"";

        bool visible = firstStroke["visible"]?.ToObject<bool>() ?? true;
        if (!visible)
            return "stroke=\"none\"";

        Color strokeColor = FigmaColorParser.ParseColorWithOpacity(firstStroke);
        float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 1f;
        string strokeAlign = nodeData["strokeAlign"]?.ToString() ?? "CENTER";

        string result = $"stroke=\"{FigmaColorParser.ColorToRGBA(strokeColor)}\" stroke-width=\"{strokeWeight}\"";

        string lineCap = GetStrokeLineCap(nodeData);
        if (!string.IsNullOrEmpty(lineCap))
        {
            result += $" stroke-linecap=\"{lineCap}\"";
        }

        string lineJoin = GetStrokeLineJoin(nodeData);
        if (!string.IsNullOrEmpty(lineJoin))
        {
            result += $" stroke-linejoin=\"{lineJoin}\"";
        }

        return result;
    }

    public static float GetStrokeWeight(JObject nodeData)
    {
        return nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;
    }

    public static string GetStrokeAlign(JObject nodeData)
    {
        return nodeData["strokeAlign"]?.ToString() ?? "CENTER";
    }

    public static Color GetStrokeColor(JObject nodeData)
    {
        JArray strokes = nodeData["strokes"] as JArray;
        if (strokes == null || strokes.Count == 0)
            return Color.clear;

        JObject firstStroke = strokes[0] as JObject;
        if (firstStroke == null)
            return Color.clear;

        return FigmaColorParser.ParseColorWithOpacity(firstStroke);
    }

    public static bool HasVisibleStroke(JObject nodeData)
    {
        JArray strokes = nodeData["strokes"] as JArray;
        if (strokes == null || strokes.Count == 0)
            return false;

        float strokeWeight = nodeData["strokeWeight"]?.ToObject<float>() ?? 0f;
        if (strokeWeight <= 0)
            return false;

        foreach (JObject stroke in strokes)
        {
            bool visible = stroke["visible"]?.ToObject<bool>() ?? true;
            if (visible)
                return true;
        }

        return false;
    }

    private static string GetStrokeLineCap(JObject nodeData)
    {
        string cap = nodeData["strokeCap"]?.ToString();
        if (string.IsNullOrEmpty(cap))
            return null;

        switch (cap.ToUpper())
        {
            case "ROUND":
                return "round";
            case "SQUARE":
                return "square";
            default:
                return "butt";
        }
    }

    private static string GetStrokeLineJoin(JObject nodeData)
    {
        string join = nodeData["strokeJoin"]?.ToString();
        if (string.IsNullOrEmpty(join))
            return null;

        switch (join.ToUpper())
        {
            case "ROUND":
                return "round";
            case "BEVEL":
                return "bevel";
            default:
                return "miter";
        }
    }
}


using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FigmaFillHandler
{
    public static Color GetSolidFillColor(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return Color.clear;

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null)
            return Color.clear;

        string fillType = firstFill["type"]?.ToString();
        if (fillType == "SOLID")
        {
            return FigmaColorParser.ParseColorWithOpacity(firstFill);
        }

        return Color.clear;
    }

    public static string GenerateSVGFill(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return "fill=\"none\"";

        JObject firstFill = fills[0] as JObject;
        if (firstFill == null)
            return "fill=\"none\"";

        string fillType = firstFill["type"]?.ToString();
        bool visible = firstFill["visible"]?.ToObject<bool>() ?? true;

        if (!visible)
            return "fill=\"none\"";

        switch (fillType)
        {
            case "SOLID":
                return GenerateSolidFill(firstFill);

            case "GRADIENT_LINEAR":
                return GenerateLinearGradientFill(firstFill, nodeData);

            case "GRADIENT_RADIAL":
                return GenerateRadialGradientFill(firstFill, nodeData);

            default:
                return "fill=\"none\"";
        }
    }

    private static string GenerateSolidFill(JObject fillData)
    {
        Color color = FigmaColorParser.ParseColorWithOpacity(fillData);
        return $"fill=\"{FigmaColorParser.ColorToRGBA(color)}\"";
    }

    private static string GenerateLinearGradientFill(JObject fillData, JObject nodeData)
    {
        StringBuilder sb = new StringBuilder();
        string gradientId = $"grad_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";

        JArray gradientStops = fillData["gradientStops"] as JArray;
        if (gradientStops == null || gradientStops.Count == 0)
            return "fill=\"none\"";

        JArray handlePositions = fillData["gradientHandlePositions"] as JArray;
        if (handlePositions != null && handlePositions.Count >= 2)
        {
            JObject startPos = handlePositions[0] as JObject;
            JObject endPos = handlePositions[1] as JObject;

            float x1 = startPos?["x"]?.ToObject<float>() ?? 0f;
            float y1 = startPos?["y"]?.ToObject<float>() ?? 0f;
            float x2 = endPos?["x"]?.ToObject<float>() ?? 1f;
            float y2 = endPos?["y"]?.ToObject<float>() ?? 1f;

            sb.AppendLine($"<defs>");
            sb.AppendLine($"  <linearGradient id=\"{gradientId}\" x1=\"{x1 * 100}%\" y1=\"{y1 * 100}%\" x2=\"{x2 * 100}%\" y2=\"{y2 * 100}%\">");

            foreach (JObject stop in gradientStops)
            {
                float position = stop["position"]?.ToObject<float>() ?? 0f;
                Color stopColor = FigmaColorParser.ParseColor(stop["color"] as JObject);
                sb.AppendLine($"    <stop offset=\"{position * 100}%\" stop-color=\"{FigmaColorParser.ColorToHex(stopColor)}\" stop-opacity=\"{stopColor.a}\" />");
            }

            sb.AppendLine($"  </linearGradient>");
            sb.AppendLine($"</defs>");
        }

        return $"fill=\"url(#{gradientId})\"||{sb.ToString()}";
    }

    private static string GenerateRadialGradientFill(JObject fillData, JObject nodeData)
    {
        StringBuilder sb = new StringBuilder();
        string gradientId = $"grad_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";

        JArray gradientStops = fillData["gradientStops"] as JArray;
        if (gradientStops == null || gradientStops.Count == 0)
            return "fill=\"none\"";

        sb.AppendLine($"<defs>");
        sb.AppendLine($"  <radialGradient id=\"{gradientId}\" cx=\"50%\" cy=\"50%\" r=\"50%\">");

        foreach (JObject stop in gradientStops)
        {
            float position = stop["position"]?.ToObject<float>() ?? 0f;
            Color stopColor = FigmaColorParser.ParseColor(stop["color"] as JObject);
            sb.AppendLine($"    <stop offset=\"{position * 100}%\" stop-color=\"{FigmaColorParser.ColorToHex(stopColor)}\" stop-opacity=\"{stopColor.a}\" />");
        }

        sb.AppendLine($"  </radialGradient>");
        sb.AppendLine($"</defs>");

        return $"fill=\"url(#{gradientId})\"||{sb.ToString()}";
    }

    public static bool HasVisibleFill(JObject nodeData)
    {
        JArray fills = nodeData["fills"] as JArray;
        if (fills == null || fills.Count == 0)
            return false;

        foreach (JObject fill in fills)
        {
            bool visible = fill["visible"]?.ToObject<bool>() ?? true;
            if (visible)
                return true;
        }

        return false;
    }
}


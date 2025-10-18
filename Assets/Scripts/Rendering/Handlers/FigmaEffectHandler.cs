// using System.Text;
// using Newtonsoft.Json.Linq;
// using UnityEngine;

// public static class FigmaEffectHandler
// {
//     public static string GenerateSVGEffects(JObject nodeData, out float padding)
//     {
//         padding = 0f;
//         JArray effects = nodeData["effects"] as JArray;

//         if (effects == null || effects.Count == 0)
//             return string.Empty;

//         StringBuilder sb = new StringBuilder();
//         string filterId = $"filter_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
//         bool hasEffects = false;

//         sb.AppendLine($"<defs>");
//         sb.AppendLine(
//             $"  <filter id=\"{filterId}\" x=\"-50%\" y=\"-50%\" width=\"200%\" height=\"200%\">"
//         );

//         foreach (JObject effect in effects)
//         {
//             bool visible = effect["visible"]?.ToObject<bool>() ?? true;
//             if (!visible)
//                 continue;

//             string effectType = effect["type"]?.ToString();

//             switch (effectType)
//             {
//                 case "DROP_SHADOW":
//                     sb.AppendLine(GenerateDropShadow(effect, ref padding));
//                     hasEffects = true;
//                     break;

//                 case "INNER_SHADOW":
//                     sb.AppendLine(GenerateInnerShadow(effect, ref padding));
//                     hasEffects = true;
//                     break;

//                 case "LAYER_BLUR":
//                     sb.AppendLine(GenerateLayerBlur(effect, ref padding));
//                     hasEffects = true;
//                     break;

//                 case "BACKGROUND_BLUR":
//                     sb.AppendLine(GenerateBackgroundBlur(effect, ref padding));
//                     hasEffects = true;
//                     break;
//             }
//         }

//         sb.AppendLine($"  </filter>");
//         sb.AppendLine($"</defs>");

//         if (!hasEffects)
//             return string.Empty;

//         return $"filter=\"url(#{filterId})\"||{sb.ToString()}";
//     }

//     public static float CalculateEffectPadding(JObject nodeData)
//     {
//         JArray effects = nodeData["effects"] as JArray;
//         if (effects == null || effects.Count == 0)
//             return 0f;

//         float maxPadding = 0f;

//         foreach (JObject effect in effects)
//         {
//             bool visible = effect["visible"]?.ToObject<bool>() ?? true;
//             if (!visible)
//                 continue;

//             string effectType = effect["type"]?.ToString();

//             switch (effectType)
//             {
//                 case "DROP_SHADOW":
//                     float radius = effect["radius"]?.ToObject<float>() ?? 0f;
//                     JObject offset = effect["offset"] as JObject;
//                     float offsetX = offset?["x"]?.ToObject<float>() ?? 0f;
//                     float offsetY = offset?["y"]?.ToObject<float>() ?? 0f;
//                     float shadowPadding =
//                         Mathf.Max(radius, Mathf.Abs(offsetX), Mathf.Abs(offsetY)) + radius;
//                     maxPadding = Mathf.Max(maxPadding, shadowPadding);
//                     break;

//                 case "LAYER_BLUR":
//                 case "BACKGROUND_BLUR":
//                     float blurRadius = effect["radius"]?.ToObject<float>() ?? 0f;
//                     maxPadding = Mathf.Max(maxPadding, blurRadius * 2f);
//                     break;
//             }
//         }

//         return maxPadding;
//     }

//     private static string GenerateDropShadow(JObject effect, ref float padding)
//     {
//         JObject offset = effect["offset"] as JObject;
//         float offsetX = offset?["x"]?.ToObject<float>() ?? 0f;
//         float offsetY = offset?["y"]?.ToObject<float>() ?? 0f;
//         float radius = effect["radius"]?.ToObject<float>() ?? 0f;
//         Color color = FigmaColorParser.ParseColor(effect["color"] as JObject);

//         float shadowPadding = Mathf.Max(radius, Mathf.Abs(offsetX), Mathf.Abs(offsetY)) + radius;
//         padding = Mathf.Max(padding, shadowPadding);

//         return $"    <feDropShadow dx=\"{offsetX}\" dy=\"{offsetY}\" stdDeviation=\"{radius}\" "
//             + $"flood-color=\"{FigmaColorParser.ColorToHex(color)}\" flood-opacity=\"{color.a}\" />";
//     }

//     private static string GenerateInnerShadow(JObject effect, ref float padding)
//     {
//         JObject offset = effect["offset"] as JObject;
//         float offsetX = offset?["x"]?.ToObject<float>() ?? 0f;
//         float offsetY = offset?["y"]?.ToObject<float>() ?? 0f;
//         float radius = effect["radius"]?.ToObject<float>() ?? 0f;
//         Color color = FigmaColorParser.ParseColor(effect["color"] as JObject);

//         float shadowPadding = Mathf.Max(radius, Mathf.Abs(offsetX), Mathf.Abs(offsetY));
//         padding = Mathf.Max(padding, shadowPadding);

//         StringBuilder sb = new StringBuilder();
//         sb.AppendLine($"    <feComponentTransfer in=\"SourceAlpha\">");
//         sb.AppendLine($"      <feFuncA type=\"table\" tableValues=\"1 0\" />");
//         sb.AppendLine($"    </feComponentTransfer>");
//         sb.AppendLine($"    <feGaussianBlur stdDeviation=\"{radius}\" />");
//         sb.AppendLine($"    <feOffset dx=\"{offsetX}\" dy=\"{offsetY}\" result=\"offsetblur\" />");
//         sb.AppendLine(
//             $"    <feFlood flood-color=\"{FigmaColorParser.ColorToHex(color)}\" flood-opacity=\"{color.a}\" />"
//         );
//         sb.AppendLine($"    <feComposite in2=\"offsetblur\" operator=\"in\" />");
//         sb.AppendLine($"    <feComposite in2=\"SourceAlpha\" operator=\"in\" />");
//         sb.AppendLine($"    <feMerge>");
//         sb.AppendLine($"      <feMergeNode />");
//         sb.AppendLine($"      <feMergeNode in=\"SourceGraphic\" />");
//         sb.AppendLine($"    </feMerge>");

//         return sb.ToString();
//     }

//     private static string GenerateLayerBlur(JObject effect, ref float padding)
//     {
//         float radius = effect["radius"]?.ToObject<float>() ?? 0f;
//         padding = Mathf.Max(padding, radius * 2f);

//         return $"    <feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"{radius}\" />";
//     }

//     private static string GenerateBackgroundBlur(JObject effect, ref float padding)
//     {
//         float radius = effect["radius"]?.ToObject<float>() ?? 0f;
//         padding = Mathf.Max(padding, radius * 2f);

//         return $"    <feGaussianBlur in=\"BackgroundImage\" stdDeviation=\"{radius}\" />";
//     }

//     public static bool HasVisibleEffects(JObject nodeData)
//     {
//         JArray effects = nodeData["effects"] as JArray;
//         if (effects == null || effects.Count == 0)
//             return false;

//         foreach (JObject effect in effects)
//         {
//             bool visible = effect["visible"]?.ToObject<bool>() ?? true;
//             if (visible)
//                 return true;
//         }

//         return false;
//     }
// }

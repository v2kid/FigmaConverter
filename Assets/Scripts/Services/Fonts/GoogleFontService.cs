using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Service for detecting fonts used in Figma data, downloading from Google Fonts API,
/// and creating TMP_FontAsset for use in the converter.
/// </summary>
public class GoogleFontService
{
    private const string GOOGLE_FONTS_API_URL = "https://www.googleapis.com/webfonts/v1/webfonts";

    private readonly FigmaConverterConfig _config;

    // fontFamily -> TMP_FontAsset mapping
    private readonly Dictionary<string, TMP_FontAsset> _fontMapping =
        new Dictionary<string, TMP_FontAsset>();

    // Fonts that were requested but not found anywhere
    private readonly List<string> _missingFonts = new List<string>();

    // Fonts that were downloaded from Google Fonts
    private readonly List<string> _downloadedFonts = new List<string>();

    public GoogleFontService(FigmaConverterConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Scans all TEXT nodes in the tree and collects unique fontFamily values
    /// </summary>
    public HashSet<string> CollectUsedFontFamilies(JToken root)
    {
        var families = new HashSet<string>();
        CollectFontsRecursive(root, families);
        Debug.Log($"🔤 Found {families.Count} unique font families: {string.Join(", ", families)}");
        return families;
    }

    private void CollectFontsRecursive(JToken token, HashSet<string> families)
    {
        if (token == null) return;

        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            string nodeType = obj["type"]?.ToString();

            if (nodeType == "TEXT")
            {
                string fontFamily = obj["style"]?["fontFamily"]?.ToString();
                if (!string.IsNullOrEmpty(fontFamily))
                {
                    families.Add(fontFamily);
                }
            }

            if (obj.TryGetValue("children", out JToken children) && children is JArray arr)
            {
                foreach (var child in arr)
                    CollectFontsRecursive(child, families);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var item in (JArray)token)
                CollectFontsRecursive(item, families);
        }
    }

    /// <summary>
    /// Resolves all font families: check local → Google Fonts → fallback
    /// </summary>
    public IEnumerator ResolveFontsCoroutine(HashSet<string> families)
    {
        _fontMapping.Clear();
        _missingFonts.Clear();
        _downloadedFonts.Clear();

        foreach (string family in families)
        {
            // Step 1: Check local Assets/Fonts/{family}/
            TMP_FontAsset localFont = FindLocalFontAsset(family);
            if (localFont != null)
            {
                _fontMapping[family] = localFont;
                Debug.Log($"✓ Font '{family}' found locally");
                continue;
            }

            // Step 2: Try Google Fonts API
            if (!string.IsNullOrEmpty(Secrets.GOOGLE_FONTS_API_KEY))
            {
                bool downloaded = false;
                yield return DownloadFromGoogleFonts(family, (success) => downloaded = success);

                if (downloaded)
                {
                    // Reload the font asset after download
                    TMP_FontAsset newFont = FindLocalFontAsset(family);
                    if (newFont != null)
                    {
                        _fontMapping[family] = newFont;
                        _downloadedFonts.Add(family);
                        Debug.Log($"✓ Font '{family}' downloaded from Google Fonts");
                        continue;
                    }
                }
            }

            // Step 3: Fallback — mark as missing
            _missingFonts.Add(family);
            if (_config.defaultFont != null)
            {
                _fontMapping[family] = _config.defaultFont;
            }
            Debug.LogWarning($"⚠ Font '{family}' not found — using fallback");
        }

        LogFontReport();
    }

    /// <summary>
    /// Gets the resolved TMP_FontAsset for a given font family
    /// </summary>
    public TMP_FontAsset GetFontAsset(string fontFamily)
    {
        if (string.IsNullOrEmpty(fontFamily))
            return _config.defaultFont;

        return _fontMapping.TryGetValue(fontFamily, out var font) ? font : _config.defaultFont;
    }

    /// <summary>
    /// Returns list of font families that could not be resolved
    /// </summary>
    public List<string> GetMissingFonts() => new List<string>(_missingFonts);

    /// <summary>
    /// Returns list of font families that were downloaded from Google Fonts
    /// </summary>
    public List<string> GetDownloadedFonts() => new List<string>(_downloadedFonts);

    /// <summary>
    /// Searches for an existing TMP_FontAsset in Assets/Fonts/
    /// </summary>
    private TMP_FontAsset FindLocalFontAsset(string fontFamily)
    {
#if UNITY_EDITOR
        string fontsDir = _config.fontsPath;

        // Ensure fonts directory exists
        if (!Directory.Exists(fontsDir))
        {
            Directory.CreateDirectory(fontsDir);
            AssetDatabase.Refresh();
            return null;
        }

        string sanitizedFamily = fontFamily.Replace(" ", "_");

        // Search in the fonts folder for any TMP_FontAsset matching the family name
        string[] guids = AssetDatabase.FindAssets($"t:TMP_FontAsset", new[] { fontsDir });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

            if (fontAsset != null)
            {
                // Match by font asset name or source font family name
                string assetName = fontAsset.name.Replace(" ", "_").Replace("-", "_");
                string familyNormalized = sanitizedFamily.Replace("-", "_");

                if (assetName.IndexOf(familyNormalized, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return fontAsset;
                }

                // Also check the source font's family name
                if (fontAsset.sourceFontFile != null)
                {
                    string sourceName = fontAsset.sourceFontFile.name.Replace(" ", "_").Replace("-", "_");
                    if (sourceName.IndexOf(familyNormalized, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return fontAsset;
                    }
                }
            }
        }

        // Also search for regular Font assets (TTF/OTF) and auto-create TMP_FontAsset
        string[] fontGuids = AssetDatabase.FindAssets($"t:Font", new[] { fontsDir });
        foreach (string guid in fontGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);

            if (font != null)
            {
                string fontName = font.name.Replace(" ", "_").Replace("-", "_");
                string familyNormalized = sanitizedFamily.Replace("-", "_");

                if (fontName.IndexOf(familyNormalized, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Auto-create TMP_FontAsset from Font
                    TMP_FontAsset tmpFont = CreateTMPFontAsset(font, fontFamily);
                    if (tmpFont != null)
                        return tmpFont;
                }
            }
        }
#endif
        return null;
    }

    /// <summary>
    /// Downloads a font from Google Fonts API
    /// </summary>
    private IEnumerator DownloadFromGoogleFonts(string fontFamily, Action<bool> onComplete)
    {
#if UNITY_EDITOR
        // Query Google Fonts API for this specific family
        string encodedFamily = UnityWebRequest.EscapeURL(fontFamily);
        string apiUrl = $"{GOOGLE_FONTS_API_URL}?family={encodedFamily}&key={Secrets.GOOGLE_FONTS_API_KEY}";

        using (var request = UnityWebRequest.Get(apiUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Google Fonts API error for '{fontFamily}': {request.error}");
                onComplete?.Invoke(false);
                yield break;
            }

            string json = request.downloadHandler.text;
            JObject response = JObject.Parse(json);
            JArray items = response["items"] as JArray;

            if (items == null || items.Count == 0)
            {
                Debug.LogWarning($"Font '{fontFamily}' not found on Google Fonts");
                onComplete?.Invoke(false);
                yield break;
            }

            // Get the first matching font family
            JObject fontItem = items[0] as JObject;
            JObject files = fontItem?["files"] as JObject;

            if (files == null)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            // Prefer "regular" variant, fallback to first available
            string downloadUrl = files["regular"]?.ToString();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // Take first available variant
                foreach (var prop in files.Properties())
                {
                    downloadUrl = prop.Value?.ToString();
                    if (!string.IsNullOrEmpty(downloadUrl))
                        break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Debug.LogWarning($"No download URL found for font '{fontFamily}'");
                onComplete?.Invoke(false);
                yield break;
            }

            // Download the TTF file
            yield return DownloadAndSaveFont(fontFamily, downloadUrl, onComplete);
        }
#else
        Debug.LogWarning("Font download from Google Fonts only works in Unity Editor");
        onComplete?.Invoke(false);
        yield break;
#endif
    }

    /// <summary>
    /// Downloads a TTF file and creates a TMP_FontAsset from it
    /// </summary>
    private IEnumerator DownloadAndSaveFont(string fontFamily, string url, Action<bool> onComplete)
    {
#if UNITY_EDITOR
        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", "Unity-FigmaConverter");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download font '{fontFamily}': {request.error}");
                onComplete?.Invoke(false);
                yield break;
            }

            byte[] fontData = request.downloadHandler.data;
            if (fontData == null || fontData.Length == 0)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            // Save TTF file
            string sanitizedFamily = fontFamily.Replace(" ", "_");
            string fontDir = Path.Combine(_config.fontsPath, sanitizedFamily);

            if (!Directory.Exists(fontDir))
                Directory.CreateDirectory(fontDir);

            string ttfPath = Path.Combine(fontDir, $"{sanitizedFamily}-Regular.ttf");
            File.WriteAllBytes(ttfPath, fontData);

            // Import the font asset
            AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // Load the imported Font
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError($"Failed to load imported font: {ttfPath}");
                onComplete?.Invoke(false);
                yield break;
            }

            // Create TMP_FontAsset
            TMP_FontAsset tmpFont = CreateTMPFontAsset(font, fontFamily);
            onComplete?.Invoke(tmpFont != null);
        }
#else
        onComplete?.Invoke(false);
        yield break;
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Creates a TMP_FontAsset from a Font and saves it
    /// </summary>
    private TMP_FontAsset CreateTMPFontAsset(Font font, string fontFamily)
    {
        try
        {
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset == null)
            {
                Debug.LogError($"Failed to create TMP_FontAsset for '{fontFamily}'");
                return null;
            }

            string sanitizedFamily = fontFamily.Replace(" ", "_");
            string fontDir = Path.Combine(_config.fontsPath, sanitizedFamily);

            if (!Directory.Exists(fontDir))
                Directory.CreateDirectory(fontDir);

            string assetPath = Path.Combine(fontDir, $"{sanitizedFamily} SDF.asset");

            // Check if already exists
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✓ Created TMP_FontAsset: {assetPath}");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating TMP_FontAsset for '{fontFamily}': {ex.Message}");
            return null;
        }
    }
#else
    private TMP_FontAsset CreateTMPFontAsset(Font font, string fontFamily)
    {
        Debug.LogWarning("TMP_FontAsset creation only works in Unity Editor");
        return null;
    }
#endif

    /// <summary>
    /// Logs a summary of font resolution results
    /// </summary>
    private void LogFontReport()
    {
        Debug.Log("═══════════════════════════════════════");
        Debug.Log("📋 FONT RESOLUTION REPORT");
        Debug.Log($"  Total resolved: {_fontMapping.Count}");
        Debug.Log($"  Downloaded from Google Fonts: {_downloadedFonts.Count}");

        if (_downloadedFonts.Count > 0)
        {
            Debug.Log($"  ✓ Downloaded: {string.Join(", ", _downloadedFonts)}");
        }

        if (_missingFonts.Count > 0)
        {
            Debug.LogWarning($"  ⚠ Missing fonts ({_missingFonts.Count}) — please download manually:");
            foreach (string font in _missingFonts)
            {
                Debug.LogWarning($"    • {font}");
            }
        }
        else
        {
            Debug.Log("  ✓ All fonts resolved successfully!");
        }
        Debug.Log("═══════════════════════════════════════");
    }
}

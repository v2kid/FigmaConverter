using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for FigmaConverter — provides grouped settings,
/// one-click actions, a progress bar, and an inline log viewer.
/// </summary>
[CustomEditor(typeof(FigmaConverter))]
[CanEditMultipleObjects]
public class FigmaConverterEditor : Editor
{
    // ─── Foldout states ───
    private bool _showUrl = true;
    private bool _showApi = true;
    private bool _showUi = true;
    private bool _showImage = true;
    private bool _showLogs = true;

    // ─── Log scroll position ───
    private Vector2 _logScroll;

    // ─── Styles (lazily initialized) ───
    private GUIStyle _headerStyle;
    private GUIStyle _logInfoStyle;
    private GUIStyle _logWarnStyle;
    private GUIStyle _logErrStyle;
    private GUIStyle _logSuccessStyle;
    private GUIStyle _logTimestampStyle;

    // ─── Serialized Properties ───
    private SerializedProperty _figmaUrl;
    private SerializedProperty _config;

    // Config sub-properties
    private SerializedProperty _fileId;
    private SerializedProperty _nodeId;
    private SerializedProperty _fontsPath;
    private SerializedProperty _targetCanvas;
    private SerializedProperty _createNewCanvas;
    private SerializedProperty _canvasName;
    private SerializedProperty _defaultFont;
    private SerializedProperty _defaultTextColor;
    private SerializedProperty _scaleFactor;
    private SerializedProperty _imageFormat;
    private SerializedProperty _imageScale;

    private void OnEnable()
    {
        _figmaUrl = serializedObject.FindProperty("figmaUrl");
        _config = serializedObject.FindProperty("config");

        if (_config != null)
        {
            _fileId = _config.FindPropertyRelative("fileId");
            _nodeId = _config.FindPropertyRelative("nodeId");
            _fontsPath = _config.FindPropertyRelative("fontsPath");
            _targetCanvas = _config.FindPropertyRelative("targetCanvas");
            _createNewCanvas = _config.FindPropertyRelative("createNewCanvas");
            _canvasName = _config.FindPropertyRelative("canvasName");
            _defaultFont = _config.FindPropertyRelative("defaultFont");
            _defaultTextColor = _config.FindPropertyRelative("defaultTextColor");
            _scaleFactor = _config.FindPropertyRelative("scaleFactor");
            _imageFormat = _config.FindPropertyRelative("imageFormat");
            _imageScale = _config.FindPropertyRelative("imageScale");
        }

        FigmaConverter.OnLogsChanged += OnLogsChanged;
    }

    private void OnDisable()
    {
        FigmaConverter.OnLogsChanged -= OnLogsChanged;
    }

    private void OnLogsChanged()
    {
        Repaint();
    }

    // ─── Lazy style init ───
    private void EnsureStyles()
    {
        if (_headerStyle != null) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin = new RectOffset(0, 0, 8, 4),
        };

        _logInfoStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            richText = true,
            fontSize = 11,
            normal = { textColor = new Color(0.78f, 0.78f, 0.78f) },
        };

        _logWarnStyle = new GUIStyle(_logInfoStyle)
        {
            normal = { textColor = new Color(1f, 0.85f, 0.3f) },
        };

        _logErrStyle = new GUIStyle(_logInfoStyle)
        {
            normal = { textColor = new Color(1f, 0.4f, 0.4f) },
        };

        _logSuccessStyle = new GUIStyle(_logInfoStyle)
        {
            normal = { textColor = new Color(0.3f, 0.9f, 0.5f) },
        };

        _logTimestampStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            fontSize = 9,
            alignment = TextAnchor.UpperRight,
        };
    }

    public override void OnInspectorGUI()
    {
        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("Multi-object editing is not supported.", MessageType.Warning);
            return;
        }

        serializedObject.Update();
        EnsureStyles();

        FigmaConverter converter = (FigmaConverter)target;

        // ═══════════════════════════════════════
        // TITLE
        // ═══════════════════════════════════════
        EditorGUILayout.Space(4);
        DrawTitle();
        EditorGUILayout.Space(4);

        // ═══════════════════════════════════════
        // FIGMA URL
        // ═══════════════════════════════════════
        _showUrl = DrawFoldoutSection("🔗  Figma URL", _showUrl, () =>
        {
            EditorGUILayout.PropertyField(_figmaUrl, new GUIContent("URL"));

            if (_fileId != null && _nodeId != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("File ID", _fileId.stringValue);
                EditorGUILayout.TextField("Node ID", _nodeId.stringValue);
                EditorGUI.EndDisabledGroup();
            }
        });

        // ═══════════════════════════════════════
        // ACTIONS
        // ═══════════════════════════════════════
        EditorGUILayout.Space(4);
        DrawActionsSection(converter);
        EditorGUILayout.Space(2);

        // ═══════════════════════════════════════
        // PROGRESS BAR
        // ═══════════════════════════════════════
        if (FigmaConverter.IsRunning || FigmaConverter.Progress > 0f)
        {
            DrawProgressBar();
        }

        // ═══════════════════════════════════════
        // API SETTINGS
        // ═══════════════════════════════════════
        _showApi = DrawFoldoutSection("🔑  API Settings", _showApi, () =>
        {
            if (_fontsPath != null)
                EditorGUILayout.PropertyField(_fontsPath, new GUIContent("Fonts Path"));
        });

        // ═══════════════════════════════════════
        // UI SETTINGS
        // ═══════════════════════════════════════
        _showUi = DrawFoldoutSection("🖥  UI Settings", _showUi, () =>
        {
            if (_targetCanvas != null) EditorGUILayout.PropertyField(_targetCanvas, new GUIContent("Target Canvas"));
            if (_createNewCanvas != null) EditorGUILayout.PropertyField(_createNewCanvas, new GUIContent("Auto-Create Canvas"));
            if (_canvasName != null) EditorGUILayout.PropertyField(_canvasName, new GUIContent("Canvas Name"));
            if (_defaultFont != null) EditorGUILayout.PropertyField(_defaultFont, new GUIContent("Default Font"));
            if (_defaultTextColor != null) EditorGUILayout.PropertyField(_defaultTextColor, new GUIContent("Default Text Color"));
            if (_scaleFactor != null) EditorGUILayout.PropertyField(_scaleFactor, new GUIContent("Scale Factor"));
        });

        // ═══════════════════════════════════════
        // IMAGE SETTINGS
        // ═══════════════════════════════════════
        _showImage = DrawFoldoutSection("🖼  Image Settings", _showImage, () =>
        {
            if (_imageFormat != null) EditorGUILayout.PropertyField(_imageFormat, new GUIContent("Format"));
            if (_imageScale != null) EditorGUILayout.PropertyField(_imageScale, new GUIContent("Scale"));
        });



        // ═══════════════════════════════════════
        // LOG VIEWER
        // ═══════════════════════════════════════
        EditorGUILayout.Space(4);
        _showLogs = DrawFoldoutSection("📋  Log Output", _showLogs, DrawLogViewer);

        serializedObject.ApplyModifiedProperties();
    }

    // ─── Drawing Helpers ───

    private void DrawTitle()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Figma → Unity Converter", new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 0.7f, 1f) },
        });
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawActionsSection(FigmaConverter converter)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUI.BeginDisabledGroup(FigmaConverter.IsRunning);

        // Main action button
        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
        if (GUILayout.Button("▶  Download & Convert", GUILayout.Height(32)))
        {
            converter.DownloadAndConvertEverything();
        }
        GUI.backgroundColor = origBg;

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    private void DrawProgressBar()
    {
        EditorGUILayout.Space(2);
        Rect progressRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(progressRect, FigmaConverter.Progress,
            FigmaConverter.ProgressLabel ?? "Processing...");
        EditorGUILayout.Space(2);
    }

    private void DrawLogViewer()
    {
        var logs = FigmaConverter.Logs;

        // Header row: count + clear button
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{logs.Count} entries", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            FigmaConverter.ClearLogs();
        }
        EditorGUILayout.EndHorizontal();

        if (logs.Count == 0)
        {
            EditorGUILayout.HelpBox("No logs yet. Click \"Download & Convert\" to start.", MessageType.Info);
            return;
        }

        // Scrollable log area
        _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MaxHeight(300));

        for (int i = logs.Count - 1; i >= 0; i--)
        {
            var entry = logs[i];
            GUIStyle style;
            switch (entry.type)
            {
                case FigmaConverter.ConverterLogType.Warning: style = _logWarnStyle; break;
                case FigmaConverter.ConverterLogType.Error: style = _logErrStyle; break;
                case FigmaConverter.ConverterLogType.Success: style = _logSuccessStyle; break;
                default: style = _logInfoStyle; break;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.message, style);
            GUILayout.Label(entry.timestamp, _logTimestampStyle, GUILayout.Width(55));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws a styled foldout section with a boxed background.
    /// </summary>
    private bool DrawFoldoutSection(string title, bool isExpanded, System.Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
        });

        if (isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);
            drawContent?.Invoke();
            EditorGUILayout.Space(2);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        return isExpanded;
    }
}

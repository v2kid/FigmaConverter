using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class FigmaUIHelper
{
    // Common UI colors
    public static readonly Color FigmaBlue = new Color(0.1f, 0.6f, 1f, 1f);
    public static readonly Color FigmaRed = new Color(1f, 0.3f, 0.3f, 1f);
    public static readonly Color FigmaGreen = new Color(0.2f, 0.8f, 0.4f, 1f);

    public static Button ConvertToButton(GameObject target, bool interactable = true)
    {
        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
        }

        button.interactable = interactable;

        // Set up button colors
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
        button.colors = colors;

        return button;
    }

    public static ScrollRect CreateScrollView(GameObject parent, Vector2 size)
    {
        GameObject scrollViewGO = new GameObject("ScrollView");
        scrollViewGO.transform.SetParent(parent.transform);

        RectTransform scrollViewRT = scrollViewGO.AddComponent<RectTransform>();
        scrollViewRT.sizeDelta = size;

        // Add Image for background
        Image scrollViewImage = scrollViewGO.AddComponent<Image>();
        scrollViewImage.color = new Color(1f, 1f, 1f, 0.1f);

        // Add ScrollRect
        ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();

        // Create Viewport
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform);

        RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        Image viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = Color.clear;

        Mask viewportMask = viewportGO.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Create Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform);

        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        // Configure ScrollRect
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        return scrollRect;
    }

    public static void ApplyAutoLayout(
        GameObject target,
        string direction = "VERTICAL",
        float spacing = 0f,
        RectOffset padding = null
    )
    {
        LayoutGroup layoutGroup = null;

        if (direction.ToUpper() == "HORIZONTAL")
        {
            HorizontalLayoutGroup horizontalGroup = target.GetComponent<HorizontalLayoutGroup>();
            if (horizontalGroup == null)
                horizontalGroup = target.AddComponent<HorizontalLayoutGroup>();
            layoutGroup = horizontalGroup;
        }
        else
        {
            VerticalLayoutGroup verticalGroup = target.GetComponent<VerticalLayoutGroup>();
            if (verticalGroup == null)
                verticalGroup = target.AddComponent<VerticalLayoutGroup>();
            layoutGroup = verticalGroup;
        }

        if (layoutGroup is HorizontalLayoutGroup horizontalGroupInstance)
        {
            horizontalGroupInstance.spacing = spacing;
            horizontalGroupInstance.childControlWidth = true;
            horizontalGroupInstance.childControlHeight = true;
            horizontalGroupInstance.childForceExpandWidth = false;
            horizontalGroupInstance.childForceExpandHeight = false;
        }
        else if (layoutGroup is VerticalLayoutGroup verticalGroupInstance)
        {
            verticalGroupInstance.spacing = spacing;
            verticalGroupInstance.childControlWidth = true;
            verticalGroupInstance.childControlHeight = true;
            verticalGroupInstance.childForceExpandWidth = false;
            verticalGroupInstance.childForceExpandHeight = false;
        }

        if (padding != null)
        {
            layoutGroup.padding = padding;
        }

        // Add ContentSizeFitter for dynamic sizing
        ContentSizeFitter csf = target.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = target.AddComponent<ContentSizeFitter>();

        if (direction.ToUpper() == "HORIZONTAL")
        {
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
        else
        {
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>
    /// Creates a grid layout
    /// </summary>
    public static GridLayoutGroup ApplyGridLayout(
        GameObject target,
        Vector2 cellSize,
        Vector2 spacing,
        int constraintCount = 1
    )
    {
        GridLayoutGroup grid = target.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = target.AddComponent<GridLayoutGroup>();

        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = constraintCount;

        return grid;
    }

    /// <summary>
    /// Sets up anchoring for responsive design
    /// </summary>
    public static void SetAnchoring(RectTransform rectTransform, AnchorPresets preset)
    {
        switch (preset)
        {
            case AnchorPresets.TopLeft:
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(0, 1);
                break;
            case AnchorPresets.TopCenter:
                rectTransform.anchorMin = new Vector2(0.5f, 1);
                rectTransform.anchorMax = new Vector2(0.5f, 1);
                break;
            case AnchorPresets.TopRight:
                rectTransform.anchorMin = new Vector2(1, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                break;
            case AnchorPresets.MiddleLeft:
                rectTransform.anchorMin = new Vector2(0, 0.5f);
                rectTransform.anchorMax = new Vector2(0, 0.5f);
                break;
            case AnchorPresets.MiddleCenter:
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                break;
            case AnchorPresets.MiddleRight:
                rectTransform.anchorMin = new Vector2(1, 0.5f);
                rectTransform.anchorMax = new Vector2(1, 0.5f);
                break;
            case AnchorPresets.BottomLeft:
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(0, 0);
                break;
            case AnchorPresets.BottomCenter:
                rectTransform.anchorMin = new Vector2(0.5f, 0);
                rectTransform.anchorMax = new Vector2(0.5f, 0);
                break;
            case AnchorPresets.BottomRight:
                rectTransform.anchorMin = new Vector2(1, 0);
                rectTransform.anchorMax = new Vector2(1, 0);
                break;
            case AnchorPresets.StretchAll:
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                break;
        }
    }

    public static GameObject CreatePrefab(GameObject source, string path, string name)
    {
#if UNITY_EDITOR
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        string fullPath = System.IO.Path.Combine(path, $"{name}.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, fullPath);

        if (prefab != null)
        {
            Debug.Log($"✓ Created prefab: {fullPath}");
        }

        return prefab;
#else
        Debug.LogWarning("Prefab creation is only available in the Unity Editor");
        return null;
#endif
    }
}

public enum AnchorPresets
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    StretchAll,
}

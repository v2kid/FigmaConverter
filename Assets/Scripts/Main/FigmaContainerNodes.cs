using System;

// DOCUMENT - Root của file Figma
[Serializable]
public class FigmaDocumentNode : FigmaNode
{
    public FigmaNode[] children; // Typically contains CANVAS nodes

    public FigmaDocumentNode()
    {
        type = FigmaNodeType.DOCUMENT;
    }
}

// CANVAS - Canvas chứa frame hoặc group  
[Serializable]
public class FigmaCanvasNode : FigmaNode
{
    public FigmaColor backgroundColor;
    public FigmaFlowStartingPoint[] flowStartingPoints;
    public FigmaPrototypeDevice prototypeDevice;
    public FigmaNode[] children; // Contains FRAME, GROUP, etc.

    public FigmaCanvasNode()
    {
        type = FigmaNodeType.CANVAS;
    }
}

// FRAME - Frame (có thể là artboard)
[Serializable]
public class FigmaFrameNode : FigmaNode
{
    // Layout properties
    public FigmaLayoutMode layoutMode = FigmaLayoutMode.NONE;
    public FigmaLayoutAlign primaryAxisAlignItems = FigmaLayoutAlign.MIN;
    public FigmaLayoutAlign counterAxisAlignItems = FigmaLayoutAlign.MIN;
    public FigmaLayoutSizingMode primaryAxisSizingMode = FigmaLayoutSizingMode.FIXED;
    public FigmaLayoutSizingMode counterAxisSizingMode = FigmaLayoutSizingMode.FIXED;
    public float paddingLeft;
    public float paddingRight;
    public float paddingTop;
    public float paddingBottom;
    public float itemSpacing;
    public float counterAxisSpacing;

    // Auto layout
    public bool layoutWrap;
    public FigmaLayoutGrid[] layoutGrids;

    // Background
    public FigmaColor backgroundColor;
    public FigmaPaint[] backgrounds;

    // Corner radius
    public float cornerRadius;
    public float topLeftRadius;
    public float topRightRadius;
    public float bottomLeftRadius;
    public float bottomRightRadius;

    // Clipping
    public bool clipsContent = true;

    // Guide properties  
    public FigmaLayoutGrid[] guides;

    public FigmaFrameNode()
    {
        type = FigmaNodeType.FRAME;
    }
}

// GROUP - Nhóm các layer
[Serializable]
public class FigmaGroupNode : FigmaNode
{
    public FigmaGroupNode()
    {
        type = FigmaNodeType.GROUP;
    }
}

// COMPONENT - Component được định nghĩa
[Serializable]
public class FigmaComponentNode : FigmaNode
{
    public string componentPropertyDefinitions;
    public string description;
    public string documentationLinks;
    public bool remote;
    public string key; // Component key for referencing

    public FigmaComponentNode()
    {
        type = FigmaNodeType.COMPONENT;
    }
}

// INSTANCE - Instance của component
[Serializable]
public class FigmaInstanceNode : FigmaNode
{
    public string componentId;
    public string componentKey;
    public FigmaComponentProperty[] componentProperties;
    public FigmaOverride[] overrides;
    public bool isExposedInstance;
    public string[] exposedInstances;

    public FigmaInstanceNode()
    {
        type = FigmaNodeType.INSTANCE;
    }
}

// COMPONENT_SET - Set chứa nhiều variants
[Serializable]
public class FigmaComponentSetNode : FigmaNode
{
    public FigmaComponentProperty[] componentPropertyDefinitions;
    public string description;
    public string documentationLinks;

    public FigmaComponentSetNode()
    {
        type = FigmaNodeType.COMPONENT_SET;
    }
}

[Serializable]
public class FigmaComponentProperty
{
    public string type;         // BOOLEAN, TEXT, INSTANCE_SWAP, VARIANT
    public string defaultValue;
    public string[] variantOptions;
    public string preferredValues;
}

[Serializable]
public class FigmaOverride
{
    public string id;
    public string overriddenFields;
    public object value;
}

[Serializable]
public class FigmaPrototypeDevice
{
    public string type;
    public float rotation;
}
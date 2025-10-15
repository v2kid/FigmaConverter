using System;

// TEXT - Text node
[Serializable]
public class FigmaTextNode : FigmaNode
{
    public string characters;
    public FigmaTypeStyle style;
    public FigmaTextAlignHorizontal textAlignHorizontal = FigmaTextAlignHorizontal.LEFT;
    public FigmaTextAlignVertical textAlignVertical = FigmaTextAlignVertical.TOP;
    public FigmaTextAutoResize textAutoResize = FigmaTextAutoResize.NONE;
    public float fontSize;
    public float letterSpacing;
    public float lineHeight;
    public float paragraphIndent;
    public float paragraphSpacing;
    public FigmaTextCase textCase = FigmaTextCase.ORIGINAL;
    public FigmaTextDecoration textDecoration = FigmaTextDecoration.NONE;
    public string fontName;
    public string fontWeight;
    public bool italic;
    public FigmaStyleOverride[] styleOverrideTable;

    public FigmaTextNode()
    {
        type = FigmaNodeType.TEXT;
    }
}

// Additional node types that might be encountered
[Serializable]
public class FigmaSectionNode : FigmaNode
{
    public FigmaSectionNode()
    {
        type = FigmaNodeType.SECTION;
    }
}

[Serializable]
public class FigmaWidgetNode : FigmaNode
{
    public string widgetId;
    public FigmaWidgetProperty[] widgetProperties;

    public FigmaWidgetNode()
    {
        type = FigmaNodeType.WIDGET;
    }
}

[Serializable]
public class FigmaEmbedNode : FigmaNode
{
    public string url;
    public string embedData;

    public FigmaEmbedNode()
    {
        type = FigmaNodeType.EMBED;
    }
}

[Serializable]
public class FigmaLinkUnfurlNode : FigmaNode
{
    public string url;

    public FigmaLinkUnfurlNode()
    {
        type = FigmaNodeType.LINK_UNFURL;
    }
}

[Serializable]
public class FigmaMediaNode : FigmaNode
{
    public string mediaData;
    public string mediaType; // IMAGE, VIDEO, AUDIO

    public FigmaMediaNode()
    {
        type = FigmaNodeType.MEDIA;
    }
}

[Serializable]
public class FigmaTableNode : FigmaNode
{
    public int rows;
    public int columns;
    public FigmaTableCell[][] cells;

    public FigmaTableNode()
    {
        type = FigmaNodeType.TABLE;
    }
}

[Serializable]
public class FigmaTableCellNode : FigmaNode
{
    public int rowIndex;
    public int columnIndex;
    public int rowSpan = 1;
    public int columnSpan = 1;

    public FigmaTableCellNode()
    {
        type = FigmaNodeType.TABLE_CELL;
    }
}

[Serializable]
public class FigmaStickyNode : FigmaNode
{
    public string authorVisible;
    public FigmaColor backgroundColor;

    public FigmaStickyNode()
    {
        type = FigmaNodeType.STICKY;
    }
}

[Serializable]
public class FigmaShapeWithTextNode : FigmaNode
{
    public FigmaTextNode textNode;
    public FigmaNode shapeNode;

    public FigmaShapeWithTextNode()
    {
        type = FigmaNodeType.SHAPE_WITH_TEXT;
    }
}

[Serializable]
public class FigmaConnectorNode : FigmaNode
{
    public string connectorStart;
    public string connectorEnd;
    public FigmaVector2[] connectorLineSegments;

    public FigmaConnectorNode()
    {
        type = FigmaNodeType.CONNECTOR;
    }
}

[Serializable]
public class FigmaWashiTapeNode : FigmaNode
{
    public FigmaWashiTapeNode()
    {
        type = FigmaNodeType.WASHI_TAPE;
    }
}

[Serializable]
public class FigmaCodeBlockNode : FigmaNode
{
    public string code;
    public string language;

    public FigmaCodeBlockNode()
    {
        type = FigmaNodeType.CODE_BLOCK;
    }
}

[Serializable]
public class FigmaStampNode : FigmaNode
{
    public FigmaStampNode()
    {
        type = FigmaNodeType.STAMP;
    }
}

// Support classes for text styling
[Serializable]
public class FigmaTypeStyle
{
    public string fontFamily;
    public string fontPostScriptName;
    public int fontWeight;
    public float fontSize;
    public FigmaTextAlignHorizontal textAlignHorizontal;
    public FigmaTextAlignVertical textAlignVertical;
    public float letterSpacing;
    public FigmaPaint[] fills;
    public string hyperlink;
    public float opentypeFlags;
    public float lineHeightPx;
    public float lineHeightPercent;
    public string lineHeightUnit;
}

[Serializable]
public class FigmaStyleOverride
{
    public int start;
    public int end;
    public FigmaTypeStyle style;
}

[Serializable]
public class FigmaWidgetProperty
{
    public string name;
    public string type;
    public object value;
}

[Serializable]
public class FigmaTableCell
{
    public FigmaNode content;
    public int rowSpan;
    public int columnSpan;
}
using System;

// RECTANGLE - Hình chữ nhật
[Serializable]
public class FigmaRectangleNode : FigmaNode
{
    public float cornerRadius;
    public float[] rectangleCornerRadii; // [topLeft, topRight, bottomRight, bottomLeft]

    public FigmaRectangleNode()
    {
        type = FigmaNodeType.RECTANGLE;
    }
}

// ELLIPSE - Hình tròn/ellipse
[Serializable]
public class FigmaEllipseNode : FigmaNode
{
    public float[] arcData; // [startingAngle, endingAngle, innerRadius]

    public FigmaEllipseNode()
    {
        type = FigmaNodeType.ELLIPSE;
    }
}

// LINE - Line đơn giản
[Serializable]
public class FigmaLineNode : FigmaNode
{
    public FigmaLineNode()
    {
        type = FigmaNodeType.LINE;
    }
}

// VECTOR - Vector path (icon, shape)
[Serializable]
public class FigmaVectorNode : FigmaNode
{
    public string fillGeometry;
    public string strokeGeometry;
    public FigmaVectorPath[] vectorPaths;
    public FigmaVectorNetwork vectorNetwork;

    public FigmaVectorNode()
    {
        type = FigmaNodeType.VECTOR;
    }
}

// STAR - Hình sao
[Serializable]
public class FigmaStarNode : FigmaNode
{
    public int count = 5; // Number of points
    public float radius;
    public float innerRadius;

    public FigmaStarNode()
    {
        type = FigmaNodeType.STAR;
    }
}

// POLYGON - Hình đa giác
[Serializable]
public class FigmaPolygonNode : FigmaNode
{
    public int count = 3; // Number of sides

    public FigmaPolygonNode()
    {
        type = FigmaNodeType.POLYGON;
    }
}

// BOOLEAN_OPERATION - Boolean shape operations
[Serializable]
public class FigmaBooleanOperationNode : FigmaNode
{
    public FigmaBooleanOperationType booleanOperation;

    public FigmaBooleanOperationNode()
    {
        type = FigmaNodeType.BOOLEAN_OPERATION;
    }
}

// SLICE - Vùng slice (dùng xuất image)
[Serializable]
public class FigmaSliceNode : FigmaNode
{
    public FigmaExportSetting[] exportSettings;

    public FigmaSliceNode()
    {
        type = FigmaNodeType.SLICE;
    }
}

// Support classes for vector data
[Serializable]
public class FigmaVectorPath
{
    public string windingRule; // EVENODD, NONZERO
    public string data; // SVG path data
}

[Serializable]
public class FigmaVectorNetwork
{
    public FigmaVectorVertex[] vertices;
    public FigmaVectorEdge[] edges;
    public FigmaVectorRegion[] regions;
}

[Serializable]
public class FigmaVectorVertex
{
    public FigmaVector2 position;
    public float strokeCap;
    public float strokeJoin;
    public float cornerRadius;
    public float handleMirroring;
}

[Serializable]
public class FigmaVectorEdge
{
    public int startVertex;
    public int endVertex;
    public FigmaVector2 tangentStart;
    public FigmaVector2 tangentEnd;
}

[Serializable]
public class FigmaVectorRegion
{
    public string windingRule; // EVENODD, NONZERO
    public int[] loops;
    public FigmaPaint[] fills;
    public string fillStyleId;
}

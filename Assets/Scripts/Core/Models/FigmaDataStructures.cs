using System;
using UnityEngine;

[Serializable]
public class FigmaVector2
{
    public float x;
    public float y;

    public FigmaVector2() { }

    public FigmaVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2 ToUnityVector2()
    {
        return new Vector2(x, y);
    }
}

[Serializable]
public class FigmaColor
{
    public float r;
    public float g;
    public float b;
    public float a;

    public FigmaColor() { }

    public FigmaColor(float r, float g, float b, float a = 1f)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public Color ToUnityColor()
    {
        return new Color(r, g, b, a);
    }
}

[Serializable]
public class FigmaRectangle
{
    public float x;
    public float y;
    public float width;
    public float height;

    public FigmaRectangle() { }

    public FigmaRectangle(float x, float y, float width, float height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public Rect ToUnityRect()
    {
        return new Rect(x, y, width, height);
    }
}

[Serializable]
public class FigmaTransform
{
    public float[][] matrix; // 2x3 transformation matrix

    public FigmaTransform()
    {
        matrix = new float[2][];
        matrix[0] = new float[3];
        matrix[1] = new float[3];
    }
}

[Serializable]
public class FigmaLayoutGrid
{
    public string pattern; // COLUMNS, ROWS, GRID
    public float sectionSize;
    public bool visible;
    public FigmaColor color;
    public string alignment; // MIN, MAX, CENTER, STRETCH
    public float gutterSize;
    public float offset;
    public int count;
}

[Serializable]
public class FigmaExportSetting
{
    public string suffix;
    public string format; // JPG, PNG, SVG, PDF
    public string constraint; // SCALE, WIDTH, HEIGHT
    public float value;
}

[Serializable]
public class FigmaPaint
{
    public FigmaPaintType type;
    public bool visible = true;
    public float opacity = 1f;
    public FigmaColor color;
    public FigmaBlendMode blendMode = FigmaBlendMode.NORMAL;

    // For gradients
    public FigmaGradientHandlePosition[] gradientHandlePositions;
    public FigmaColorStop[] gradientStops;

    // For images
    public string imageRef;
    public FigmaScaleMode scaleMode = FigmaScaleMode.FILL;
    public FigmaTransform imageTransform;
    public float scalingFactor = 1f;
    public FigmaImageFilters filters;
}

[Serializable]
public class FigmaGradientHandlePosition
{
    public FigmaVector2 position;
}

[Serializable]
public class FigmaColorStop
{
    public FigmaColor color;
    public float position;
}

[Serializable]
public class FigmaImageFilters
{
    public float exposure;
    public float contrast;
    public float saturation;
    public float temperature;
    public float tint;
    public float highlights;
    public float shadows;
}

[Serializable]
public class FigmaEffect
{
    public FigmaEffectType type;
    public bool visible = true;
    public float radius;
    public FigmaColor color;
    public FigmaBlendMode blendMode = FigmaBlendMode.NORMAL;
    public FigmaVector2 offset;
    public float spread;
    public bool showShadowBehindNode = true;
}

[Serializable]
public class FigmaConstraint
{
    public string type; // SCALE, WIDTH, HEIGHT
    public float value;
}

[Serializable]
public class FigmaLayoutConstraint
{
    public string vertical; // TOP, BOTTOM, CENTER, TOP_BOTTOM, SCALE
    public string horizontal; // LEFT, RIGHT, CENTER, LEFT_RIGHT, SCALE
}

[Serializable]
public class FigmaFlowStartingPoint
{
    public string nodeId;
    public string name;
}

using System;

[Serializable]
public enum FigmaBooleanOperationType
{
    UNION, // Hợp
    SUBTRACT, // Trừ
    INTERSECT, // Giao
    EXCLUDE, // Loại trừ
}

[Serializable]
public enum FigmaBlendMode
{
    // Normal blend modes
    PASS_THROUGH,
    NORMAL,

    // Darken blend modes
    DARKEN,
    MULTIPLY,
    LINEAR_BURN,
    COLOR_BURN,

    // Lighten blend modes
    LIGHTEN,
    SCREEN,
    LINEAR_DODGE,
    COLOR_DODGE,

    // Contrast blend modes
    OVERLAY,
    SOFT_LIGHT,
    HARD_LIGHT,

    // Inversion blend modes
    DIFFERENCE,
    EXCLUSION,

    // Component blend modes
    HUE,
    SATURATION,
    COLOR,
    LUMINOSITY,
}

[Serializable]
public enum FigmaLayoutMode
{
    NONE,
    HORIZONTAL,
    VERTICAL,
}

[Serializable]
public enum FigmaLayoutAlign
{
    MIN,
    CENTER,
    MAX,
    STRETCH,
}

[Serializable]
public enum FigmaLayoutSizingMode
{
    FIXED,
    HUG,
    FILL,
}

[Serializable]
public enum FigmaTextAlignHorizontal
{
    LEFT,
    RIGHT,
    CENTER,
    JUSTIFIED,
}

[Serializable]
public enum FigmaTextAlignVertical
{
    TOP,
    CENTER,
    BOTTOM,
}

[Serializable]
public enum FigmaTextAutoResize
{
    NONE,
    WIDTH_AND_HEIGHT,
    HEIGHT,
}

[Serializable]
public enum FigmaTextDecoration
{
    NONE,
    UNDERLINE,
    STRIKETHROUGH,
}

[Serializable]
public enum FigmaTextCase
{
    ORIGINAL,
    UPPER,
    LOWER,
    TITLE,
}

[Serializable]
public enum FigmaPaintType
{
    SOLID,
    GRADIENT_LINEAR,
    GRADIENT_RADIAL,
    GRADIENT_ANGULAR,
    GRADIENT_DIAMOND,
    IMAGE,
    EMOJI,
    VIDEO,
}

[Serializable]
public enum FigmaScaleMode
{
    FILL,
    FIT,
    CROP,
    TILE,
}

[Serializable]
public enum FigmaImageType
{
    PNG,
    JPG,
    SVG,
    PDF,
}

[Serializable]
public enum FigmaConstraintType
{
    SCALE,
    WIDTH,
    HEIGHT,
}

[Serializable]
public enum FigmaLayoutPositioning
{
    AUTO,
    ABSOLUTE,
}

[Serializable]
public enum FigmaStrokeCap
{
    NONE,
    ROUND,
    SQUARE,
    ARROW_LINES,
    ARROW_EQUILATERAL,
}

[Serializable]
public enum FigmaStrokeJoin
{
    MITER,
    BEVEL,
    ROUND,
}

[Serializable]
public enum FigmaStrokeAlign
{
    INSIDE,
    OUTSIDE,
    CENTER,
}

[Serializable]
public enum FigmaEffectType
{
    INNER_SHADOW,
    DROP_SHADOW,
    LAYER_BLUR,
    BACKGROUND_BLUR,
}

using System;
using System.Collections.Generic;

[Serializable]
public class FigmaNode
{
    public string id;
    public string name;
    public FigmaNodeType type;
    public bool visible = true;
    public bool locked = false;
    public FigmaNode[] children;
    public string pluginData;
    public string sharedPluginData;

    // Layout properties
    public FigmaRectangle absoluteBoundingBox;
    public FigmaRectangle absoluteRenderBounds;
    public FigmaLayoutConstraint constraints;
    public FigmaLayoutPositioning layoutPositioning = FigmaLayoutPositioning.AUTO;
    public FigmaTransform relativeTransform;

    // Visual properties
    public FigmaPaint[] fills;
    public FigmaPaint[] strokes;
    public float strokeWeight;
    public string strokeCap;
    public string strokeJoin;
    public float[] strokeDashes;
    public float strokeMiterAngle;
    public FigmaStrokeAlign strokeAlign = FigmaStrokeAlign.INSIDE;
    public FigmaEffect[] effects;
    public float opacity = 1f;
    public FigmaBlendMode blendMode = FigmaBlendMode.PASS_THROUGH;
    public bool isMask;
    public bool isMaskOutline;

    // Export settings
    public FigmaExportSetting[] exportSettings;

    // Reactions/interactions
    public FigmaReaction[] reactions;

    // Prototype related
    public FigmaTransition transitionNodeID;
    public float transitionDuration;
    public string transitionEasing;

    public virtual void ProcessNode()
    {
        // Base processing logic
    }
}

[Serializable]
public class FigmaReaction
{
    public FigmaTrigger[] trigger;
    public FigmaAction[] actions;
}

[Serializable]
public class FigmaTrigger
{
    public string type;     // ON_CLICK, ON_HOVER, ON_PRESS, etc.
    public float delay;
}

[Serializable]
public class FigmaAction
{
    public string type;         // NODE, URL, BACK, CLOSE
    public string destinationId;
    public string navigation;   // NAVIGATE, SWAP, OVERLAY, SCROLL_TO
    public FigmaTransition transition;
    public bool preserveScrollPosition;
    public FigmaOverlaySettings overlay;
}

[Serializable]
public class FigmaTransition
{
    public string type;         // DISSOLVE, SMART_ANIMATE, MOVE_IN, MOVE_OUT, PUSH, SLIDE_IN, SLIDE_OUT
    public float duration;
    public string easing;       // EASE_IN, EASE_OUT, EASE_IN_AND_OUT, LINEAR, GENTLE_SPRING
    public string direction;    // LEFT, RIGHT, TOP, BOTTOM
}

[Serializable]
public class FigmaOverlaySettings
{
    public string overlayBackgroundInteraction;    // NONE, CLOSE_ON_CLICK_OUTSIDE
    public FigmaColor overlayBackgroundColor;
}
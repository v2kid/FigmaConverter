# Image API Approach - Simplified Version

## 🎯 Core Principle

**Download pre-rendered images from Figma, don't try to generate them.**

## Changes Made

### 1. Simplified `ApplyCombinedIconSprite()`

**Before (SVG generation attempt):**
```csharp
// Try to generate SVG from vector data
string svgString = FigmaIconDetector.GenerateCombinedSVG(nodeData, width, height);
Sprite sprite = SVGSpriteConverter.ConvertSVGToSprite(svgString, width, height);
```

**After (Image API only):**
```csharp
// Just load the downloaded image
Sprite iconSprite = Resources.Load<Sprite>($"Sprites/{nodeIdForPath}/{sanitizedName}");
image.sprite = iconSprite;
```

### 2. Simplified `ApplyVectorSprite()`

**Before:**
```csharp
// Try FigmaVectorHandler
Sprite sprite = FigmaVectorHandler.GenerateSpriteFromVector(nodeData, width, height);
```

**After:**
```csharp
// Try loading downloaded image first, fallback to basic sprite
Sprite loadedSprite = Resources.Load<Sprite>($"Sprites/{nodeIdForPath}/{sanitizedName}");
if (loadedSprite == null)
    ApplyStyledSpriteOrFills(nodeData, image, width, height);
```

## Workflow

### Phase 1: Download
```
1. Extract file/node IDs from URL
2. Download node JSON data
3. Collect image nodes:
   - Nodes with IMAGE_ prefix
   - Icon frames (FRAME with only VECTOR children)
4. Call Figma Image API to download them as PNG
5. Save to Assets/Resources/Sprites/{nodeId}/
```

### Phase 2: Convert
```
1. Process node tree
2. For each node:
   - Icon frame? → Load downloaded image
   - IMAGE_ prefix? → Load downloaded image
   - Regular frame with fills? → Generate sprite with corner radius
   - Vector? → Try load, fallback to basic sprite
   - Text? → Create TextMeshPro
```

## Node Type Handling

| Figma Node | Detection | Method | Corner Radius |
|------------|-----------|--------|---------------|
| **Icon Frame** | All children are VECTOR | Download PNG | ❌ (baked) |
| **IMAGE_ node** | Name starts with IMAGE_ | Download PNG | ❌ (baked) |
| **Regular FRAME** | Has fills or mixed children | Generate sprite | ✅ |
| **RECTANGLE/ELLIPSE** | Basic shape | Generate sprite | ✅ |
| **Single VECTOR** | Individual vector | Load or fallback | ⚠️ |
| **TEXT** | Text node | TextMeshPro | N/A |

## Example: Info Card

```json
{
  "name": "Info card",
  "type": "FRAME",
  "cornerRadius": 10.0,
  "fills": [{"type": "SOLID", "opacity": 0.3, "color": {...}}],
  "children": [
    {"type": "FRAME", "name": "header"},
    {"type": "TEXT", "name": "title"}
  ]
}
```

**Processing:**
1. Not an icon frame (has TEXT children)
2. Has fills → Generate sprite
3. `DirectSpriteGenerator.GenerateSpriteFromNodeDirect()`
4. `FigmaShapeRenderer.RenderRectangle()` with cornerRadius=10
5. Apply to Image component

**Result:**
- Rounded corners visible ✅
- Black with 30% opacity background ✅

## Example: Icon Frame

```json
{
  "name": "ri:sword-fill",
  "type": "FRAME",
  "children": [
    {"type": "VECTOR", "name": "blade"},
    {"type": "VECTOR", "name": "handle"}
  ]
}
```

**Processing:**
1. Is icon frame (all VECTOR children)
2. Downloaded as PNG via Image API
3. Load from `Sprites/{nodeId}/ri-sword-fill`
4. Apply to Image component
5. Skip processing children

**Result:**
- Perfect icon rendering ✅
- Single GameObject ✅
- No separate child objects ✅

## Dependencies

### Required:
- `FigmaApi` - Image download
- `DirectSpriteGenerator` - Basic sprite generation
- `FigmaShapeRenderer` - Corner radius rendering
- `FigmaIconDetector` - Icon frame detection

### Not Required (simplified away):
- ~~`FigmaVectorHandler`~~ - SVG from vector paths
- ~~`SVGSpriteConverter`~~ - SVG to sprite
- ~~`NodeSpriteGenerator`~~ - Complex SVG generation

## Corner Radius Debug

If you see squares instead of rounded rectangles:

### Check 1: Is it being generated?
```
Enable: enableDebugLogs = true
Look for: "🟡 Info card: Using cornerRadius = 10"
If missing: Node has no fills OR is treated as icon frame
```

### Check 2: Is it rendering?
```
Look for: "🔵 Rendering Info card: cornerRadius=10..."
If missing: DirectSpriteGenerator not used
```

### Check 3: Is corner radius too small?
```
Log shows: "scaledRadius=2.5"
If < 3px: Might be invisible at texture resolution
Solution: Increase corner radius in Figma
```

### Check 4: Is it an icon frame?
```
Look for: "✓ Created combined icon: Info card"
If found: Being treated as icon (downloaded image)
Solution: Add non-vector child to prevent icon detection
```

## Quick Test

Create in Figma:
```
RECTANGLE: "test-corner"
├─ Size: 100x100
├─ Corner Radius: 20
└─ Fill: Red solid
```

Expected logs:
```
Processing node: test-corner (Type: RECTANGLE)
🟡 test-corner: Using cornerRadius = 20
🔵 Rendering test-corner: cornerRadius=20, texture=128x128
✓ Saved: test-corner.png
```

Expected result: Red square with clearly visible rounded corners ✅

## Summary

**Simple = Better**

1. Use Figma's renderer (Image API) when possible
2. Only generate sprites for basic styled shapes
3. Don't try to reconstruct complex vector paths
4. Corner radius works on generated sprites only
5. Icon frames = downloaded images (no corner control)


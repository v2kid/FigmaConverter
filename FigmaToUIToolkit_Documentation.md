# Figma to UI Toolkit Converter Documentation

## Overview

The `FigmaNodeDataConverterUIToolkit` is a UI Toolkit version of the original UGUI converter. It converts Figma design data into Unity's modern UI Toolkit (UIElements) system instead of the legacy UGUI system.

## Key Differences from UGUI Version

### Architecture
- **UGUI Version**: Uses GameObjects with RectTransform components
- **UI Toolkit Version**: Uses VisualElement hierarchy with USS styling

### Components
| UGUI | UI Toolkit | Notes |
|------|------------|--------|
| Canvas | UIDocument | Root container for UI |
| RectTransform | VisualElement.style | Layout and positioning |
| Image | VisualElement + backgroundColor | Background colors and images |
| TextMeshPro | Label | Text rendering |
| Canvas Scaler | USS media queries | Responsive design |

### Positioning System
- **UGUI**: Anchors, pivots, and RectTransform positioning
- **UI Toolkit**: Absolute positioning with left/top/width/height or flexbox layout

### Styling
- **UGUI**: Component properties set via code
- **UI Toolkit**: USS (Unity Style Sheets) for consistent styling

## Setup Instructions

### 1. Basic Setup
1. Create a GameObject in your scene
2. Add the `FigmaNodeDataConverterUIToolkit` component
3. Assign your `FigmaNodeDataAsset` to the `nodeDataAsset` field
4. Set the `targetNodeId` to the Figma node you want to convert

### 2. UI Document Setup
You have two options:
- **Auto-create**: Set `createNewUIDocument = true` (recommended for new projects)
- **Manual**: Create a UIDocument component and assign it to `targetUIDocument`

### 3. Styling Setup
1. Import the provided `FigmaUIToolkit.uss` file into your project
2. Assign it to the `defaultStyleSheet` field in the converter
3. Customize the USS file as needed for your design requirements

### 4. Font Setup
1. Create a FontDefinition asset or use an existing one
2. Assign it to the `defaultFontDefinition` field
3. Set the `defaultTextColor` for fallback text styling

## Usage

### Converting Figma Nodes
1. Set up the component as described above
2. Right-click the component in the Inspector
3. Select "Convert Node to UI" from the context menu
4. The converter will process the Figma data and create UI Toolkit elements

### Context Menu Options
- **Convert Node to UI**: Main conversion function
- **Clear Created UI**: Removes all generated UI elements
- **List Available Nodes**: Shows all nodes available in the asset
- **Validate Setup**: Checks if all required components are properly configured
- **Debug Positioning**: Outputs detailed positioning information to console
- **Generate USS Classes**: Outputs USS class templates to console

## Advanced Features

### Custom USS Classes
Each converted element automatically gets CSS classes based on its Figma type:
- `.figma-element` - Base class for all elements
- `.figma-frame`, `.figma-text`, `.figma-rectangle`, etc. - Type-specific classes
- Element name as additional class for targeted styling

### Responsive Design
UI Toolkit inherently supports responsive design through:
- Percentage-based sizing
- Flexbox layout
- USS media queries
- Automatic DPI scaling

### Performance Benefits
- Retained mode rendering (vs immediate mode in UGUI)
- Better batching and reduced draw calls
- More efficient memory usage
- Built-in support for high-DPI displays

## Styling with USS

### Basic Element Targeting
```css
/* Style all Figma text elements */
.figma-text {
    font-size: 16px;
    color: #333333;
}

/* Style specific element by name */
.my-button {
    background-color: #007acc;
    border-radius: 8px;
}
```

### Layout Examples
```css
/* Container with flex layout */
.figma-frame {
    flex-direction: column;
    align-items: center;
    justify-content: space-around;
}

/* Responsive sizing */
.figma-container {
    width: 100%;
    min-height: 200px;
}
```

### Animations and Interactions
```css
/* Hover effects */
.interactive-element:hover {
    transform: scale(1.05);
    background-color: #0088ff;
    transition-duration: 0.3s;
}

/* Focus states for accessibility */
.interactive-element:focus {
    border-width: 2px;
    border-color: #007acc;
}
```

## Limitations and Considerations

### Current Limitations
1. **Gradients**: Limited gradient support - converts to solid color from first gradient stop
2. **Complex Shapes**: Vector shapes are simplified to basic rectangles with fills
3. **Blend Modes**: Figma blend modes are not supported
4. **Shadows**: Drop shadows and effects are not implemented
5. **Auto Layout**: Figma's auto-layout is not directly converted to flexbox

### Workarounds
1. **Custom USS**: Use custom USS for advanced styling not supported by the converter
2. **Post-processing**: Manually adjust generated elements for complex requirements
3. **Hybrid Approach**: Combine converted elements with manually created UI Toolkit components

## Migration from UGUI Version

### Code Changes Required
1. Change component type from `FigmaNodeDataConverter` to `FigmaNodeDataConverterUIToolkit`
2. Update any direct GameObject references to VisualElement references
3. Replace UGUI-specific styling with USS
4. Update any canvas-related setup code

### Asset Changes
1. Replace Canvas prefabs with UIDocument assets
2. Convert fonts from FontAsset to FontDefinition
3. Create USS files for styling instead of material-based approaches

## Performance Optimization Tips

### USS Best Practices
1. Use class selectors instead of element selectors when possible
2. Minimize deep selector nesting
3. Group related styles into reusable classes
4. Use CSS variables for consistent theming

### Layout Optimization
1. Prefer flexbox over absolute positioning when possible
2. Use `overflow: hidden` judiciously to avoid unnecessary clipping
3. Consider element hierarchy depth for complex layouts
4. Use `display: none` instead of `visibility: hidden` for better performance

### Memory Management
1. Clear unused UI elements regularly using `ClearCreatedUI()`
2. Dispose of unused VisualElements properly
3. Avoid creating excessive temporary elements during conversion

## Troubleshooting

### Common Issues
1. **Elements not visible**: Check display style and parent hierarchy
2. **Positioning incorrect**: Verify scale factor and parent positioning
3. **Styling not applied**: Ensure USS file is properly assigned and imported
4. **Font not rendering**: Check FontDefinition setup and fallback fonts

### Debug Tools
1. Use `Debug Positioning` context menu for position analysis
2. Enable debug logs for detailed conversion information
3. Use UI Toolkit Debugger in Unity Editor for runtime inspection
4. Check browser developer tools techniques for USS debugging

## Future Enhancements

### Planned Features
1. Better gradient support using custom shaders
2. Shadow and effect conversion
3. Auto-layout to flexbox conversion
4. Animation timeline support
5. Component binding system for interactive elements

### Extension Points
The converter is designed to be extensible:
1. Override node processing methods for custom behavior
2. Add custom USS class generation
3. Implement custom VisualElement types for specific needs
4. Create specialized converters for specific Figma patterns
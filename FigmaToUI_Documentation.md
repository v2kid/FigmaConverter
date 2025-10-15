# Figma to Unity UI Converter

This system converts Figma designs into Unity UI GameObjects, creates Canvas, and saves them as prefabs with TextMeshPro support for text elements.

## Components

### 1. FigmaDownloader
- Downloads Figma data from API
- Caches data in Resources/FigmaData folder
- Avoids re-downloading if cached file exists
- Supports both file and node downloads

### 2. FigmaToUIConverter
- Converts Figma JSON data to Unity UI GameObjects
- Creates appropriate UI components based on Figma node types
- Uses TextMeshPro for all text elements
- Automatically creates Canvas if needed
- Applies transforms, colors, and styling

### 3. FigmaPrefabManager
- Creates and manages prefabs from converted UI
- Organizes prefabs by type (Text, Image, Button, etc.)
- Supports prefab variants
- Can create Unity packages of generated prefabs

### 4. FigmaUIHelper
- Utility functions for UI creation
- Auto layout support (similar to Figma's auto layout)
- Anchor preset management
- Shadow and outline effects

### 5. FigmaConverterSetup
- Example setup script
- Auto-configuration of components
- Default asset assignment

## Usage

### Basic Setup

1. **Create GameObject**: Create an empty GameObject in your scene
2. **Add Components**: Add `FigmaConverterSetup` script to the GameObject
3. **Configure**: Set your Figma API token, file ID, and node ID in the FigmaDownloader
4. **Assign Assets**: Assign default font and UI material if needed
5. **Run**: Use "Download and Convert" context menu or call from code

### Manual Setup

```csharp
// Add components to GameObject
var downloader = gameObject.AddComponent<FigmaDownloader>();
var converter = gameObject.AddComponent<FigmaToUIConverter>();
var prefabManager = gameObject.AddComponent<FigmaPrefabManager>();

// Configure downloader
downloader.figmaToken = "your_figma_token";
downloader.fileId = "your_file_id";
downloader.nodeId = "your_node_id";

// Configure converter
converter.createNewCanvas = true;
converter.saveToPrefabs = true;
converter.defaultFont = yourTMPFont;

// Start conversion
StartCoroutine(ConvertFigmaToUI());
```

## Figma Node Type Mapping

| Figma Node Type | Unity Component | Notes |
|----------------|----------------|--------|
| FRAME | GameObject + Image | Container with optional background |
| GROUP | GameObject | Container for grouping |
| TEXT | TextMeshProUGUI | Uses TextMeshPro for all text |
| RECTANGLE | Image | Basic UI Image |
| ELLIPSE | Image | Circular/oval shapes |
| VECTOR | Image | Vector graphics as images |
| COMPONENT | GameObject + components | Figma components |
| INSTANCE | GameObject + components | Component instances |

## Features

### Text Support
- **TextMeshPro Integration**: All text uses TextMeshPro for better quality
- **Font Styling**: Supports font size, weight, alignment
- **Color Application**: Applies Figma text colors
- **Multi-line Support**: Handles text wrapping and alignment

### Layout Support
- **Transform Mapping**: Converts Figma positions to Unity RectTransform
- **Scale Factor**: Adjustable scale for different screen sizes
- **Anchor Points**: Smart anchor point assignment
- **Auto Layout**: Simulates Figma's auto layout with Layout Groups

### Prefab Management
- **Individual Prefabs**: Creates prefab for each converted node
- **Canvas Prefab**: Saves entire Canvas as prefab
- **Organization**: Organizes prefabs by type in folders
- **Variants**: Support for prefab variants
- **Packaging**: Can create Unity packages of all prefabs

### Caching System
- **API Efficiency**: Downloads only when needed
- **Local Storage**: Saves JSON in Resources/FigmaData
- **Force Refresh**: Option to force re-download
- **Cache Management**: Clear, show info, selective deletion

## Configuration Options

### FigmaDownloader
```csharp
public string figmaToken;           // Your Figma API token
public string fileId;              // Figma file ID
public string nodeId;              // Specific node to download
public string figmaUrl;            // Full Figma URL (auto-extracts IDs)
public bool forceRedownload;       // Force API call even if cached
public bool enableVerboseLogging; // Detailed debug logs
```

### FigmaToUIConverter
```csharp
public Canvas targetCanvas;        // Target canvas for UI
public bool createNewCanvas;       // Auto-create canvas
public string canvasName;          // Name for created canvas
public bool saveToPrefabs;         // Save results as prefabs
public TMP_FontAsset defaultFont;  // Default TextMeshPro font
public Color defaultTextColor;     // Default text color
public float scaleFactor;          // Scale adjustment
```

### FigmaPrefabManager
```csharp
public string prefabBasePath;      // Base folder for prefabs
public bool organizePrefabsByType; // Organize in type folders
public bool createVariants;        // Create prefab variants
public string prefabPrefix;        // Prefix for prefab names
```

## Context Menu Actions

### FigmaDownloader
- **Extract File ID from URL**: Auto-extract file ID from Figma URL
- **Test Token**: Verify API token validity
- **Download File Now**: Download entire Figma file
- **Download Node Now**: Download specific node
- **Clear Cache**: Remove all cached files
- **Force Redownload**: Re-download ignoring cache
- **Convert to UI**: Start UI conversion process

### FigmaToUIConverter
- **Convert Current Figma Data**: Convert cached data to UI
- **Clear Created UI**: Remove all created UI elements
- **Save Canvas to Prefab**: Save canvas as prefab

### FigmaPrefabManager
- **Organize Prefabs by Type**: Move prefabs to type folders
- **Create Prefab Package**: Export all prefabs as Unity package
- **Clear Generated Prefabs**: Clear prefab list
- **Delete Generated Prefabs**: Delete actual prefab files

## Best Practices

1. **Font Setup**: Always assign a default TextMeshPro font
2. **Scale Testing**: Test different scale factors for your target resolution
3. **Cache Management**: Clear cache when design changes significantly
4. **Prefab Organization**: Enable type-based organization for large projects
5. **Canvas Settings**: Configure Canvas Scaler for responsive design

## Troubleshooting

### Common Issues

1. **Missing Text**: Ensure TextMeshPro is imported and default font is assigned
2. **Layout Issues**: Check scale factor and canvas scaler settings
3. **API Errors**: Verify token permissions and file/node IDs
4. **Prefab Creation**: Ensure target folders exist and have write permissions

### Error Messages

- **"FigmaDownloader component not found"**: Add FigmaDownloader to GameObject
- **"Invalid token format"**: Check Figma token format (should start with "figd_")
- **"File not found"**: Verify file ID and token permissions
- **"No cached data"**: Download Figma data first before conversion

## Performance Notes

- **Caching**: First download may be slow, subsequent loads are fast
- **Large Files**: Consider downloading specific nodes rather than entire files
- **Memory**: Large Figma files may consume significant memory during conversion
- **Batch Operations**: Use individual prefab creation sparingly for large designs

## Extension Points

The system is designed to be extensible:

1. **Custom Node Processors**: Add support for new Figma node types
2. **Enhanced Styling**: Implement gradients, shadows, complex effects
3. **Animation Support**: Add timeline and animation conversion
4. **Asset Integration**: Automatically download and apply Figma images
5. **Layout Algorithms**: Implement more sophisticated layout conversion
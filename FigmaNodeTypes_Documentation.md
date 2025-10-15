# Figma Node Types Serialization System

This system provides comprehensive serialization support for all Figma node types. Each node type corresponds to a specific element or container in Figma designs.

## Supported Node Types

### Core Container Nodes

#### DOCUMENT
- **Class**: `FigmaDocumentNode`
- **Description**: Root của file Figma
- **Contains**: Canvas nodes
- **Key Properties**: Basic node properties only

#### CANVAS  
- **Class**: `FigmaCanvasNode`
- **Description**: Canvas chứa frame hoặc group
- **Contains**: Frames, groups, and other top-level elements
- **Key Properties**: 
  - `backgroundColor`: Canvas background color
  - `flowStartingPoints`: Prototype flow starting points
  - `prototypeDevice`: Device settings for prototyping

#### FRAME
- **Class**: `FigmaFrameNode` 
- **Description**: Frame (có thể là artboard)
- **Contains**: Any child elements
- **Key Properties**:
  - `layoutMode`: Auto layout mode (NONE, HORIZONTAL, VERTICAL)
  - `primaryAxisAlignItems`: Main axis alignment
  - `counterAxisAlignItems`: Cross axis alignment  
  - `paddingLeft/Right/Top/Bottom`: Padding values
  - `itemSpacing`: Gap between items
  - `cornerRadius`: Corner radius
  - `backgroundColor`: Background color
  - `clipsContent`: Whether content is clipped

#### GROUP
- **Class**: `FigmaGroupNode`
- **Description**: Nhóm các layer
- **Contains**: Any child elements grouped together
- **Key Properties**: Basic node properties only

### Component System Nodes

#### COMPONENT
- **Class**: `FigmaComponentNode`
- **Description**: Component được định nghĩa (master component)
- **Contains**: Component definition elements
- **Key Properties**:
  - `key`: Unique component identifier
  - `description`: Component description
  - `componentPropertyDefinitions`: Property definitions
  - `documentationLinks`: Links to documentation

#### INSTANCE
- **Class**: `FigmaInstanceNode` 
- **Description**: Instance của component
- **Contains**: Component instance with overrides
- **Key Properties**:
  - `componentId`: Reference to master component
  - `componentKey`: Component key reference
  - `componentProperties`: Instance property values
  - `overrides`: Property overrides
  - `isExposedInstance`: Whether instance is exposed

#### COMPONENT_SET
- **Class**: `FigmaComponentSetNode`
- **Description**: Set chứa nhiều variants (như button variants)
- **Contains**: Multiple component variants
- **Key Properties**:
  - `componentPropertyDefinitions`: Variant property definitions
  - `description`: Component set description

### Shape Nodes

#### RECTANGLE
- **Class**: `FigmaRectangleNode`
- **Description**: Hình chữ nhật
- **Key Properties**:
  - `cornerRadius`: Uniform corner radius
  - `rectangleCornerRadii`: Individual corner radii [topLeft, topRight, bottomRight, bottomLeft]

#### ELLIPSE
- **Class**: `FigmaEllipseNode`
- **Description**: Hình tròn/ellipse
- **Key Properties**:
  - `arcData`: Arc information [startingAngle, endingAngle, innerRadius]

#### LINE
- **Class**: `FigmaLineNode`
- **Description**: Line đơn giản
- **Key Properties**: Basic node properties, stroke settings

#### VECTOR
- **Class**: `FigmaVectorNode`
- **Description**: Vector path (icon, shape phức tạp)
- **Key Properties**:
  - `fillGeometry`: Fill path geometry
  - `strokeGeometry`: Stroke path geometry  
  - `vectorPaths`: Vector path data
  - `vectorNetwork`: Vector network information

#### STAR
- **Class**: `FigmaStarNode`
- **Description**: Hình sao
- **Key Properties**:
  - `count`: Number of star points (default: 5)
  - `radius`: Outer radius
  - `innerRadius`: Inner radius

#### POLYGON
- **Class**: `FigmaPolygonNode`
- **Description**: Hình đa giác
- **Key Properties**:
  - `count`: Number of polygon sides (default: 3)

#### BOOLEAN_OPERATION
- **Class**: `FigmaBooleanOperationNode`
- **Description**: Boolean shape (Union, Subtract, Intersect, Exclude)
- **Contains**: Child shapes being combined
- **Key Properties**:
  - `booleanOperation`: Type of boolean operation (UNION, SUBTRACT, INTERSECT, EXCLUDE)

### Text Node

#### TEXT
- **Class**: `FigmaTextNode`
- **Description**: Text element
- **Key Properties**:
  - `characters`: Text content
  - `style`: Text style information
  - `textAlignHorizontal`: Horizontal alignment (LEFT, RIGHT, CENTER, JUSTIFIED)
  - `textAlignVertical`: Vertical alignment (TOP, CENTER, BOTTOM)
  - `textAutoResize`: Auto resize mode (NONE, WIDTH_AND_HEIGHT, HEIGHT)
  - `fontSize`: Font size
  - `letterSpacing`: Letter spacing
  - `lineHeight`: Line height
  - `fontName`: Font family name
  - `fontWeight`: Font weight
  - `italic`: Italic style
  - `textCase`: Text case transformation
  - `textDecoration`: Text decoration (underline, strikethrough)

### Utility Nodes

#### SLICE
- **Class**: `FigmaSliceNode`
- **Description**: Vùng slice (dùng xuất image)
- **Key Properties**:
  - `exportSettings`: Export configuration for the slice

### Extended Node Types

#### SECTION
- **Class**: `FigmaSectionNode`
- **Description**: Section container for organization
- **Contains**: Organized content sections

#### WIDGET
- **Class**: `FigmaWidgetNode`  
- **Description**: Widget component
- **Key Properties**:
  - `widgetId`: Widget identifier
  - `widgetProperties`: Widget configuration

#### EMBED
- **Class**: `FigmaEmbedNode`
- **Description**: Embedded content
- **Key Properties**:
  - `url`: Embedded URL
  - `embedData`: Embed configuration

#### LINK_UNFURL
- **Class**: `FigmaLinkUnfurlNode`
- **Description**: Link preview
- **Key Properties**:
  - `url`: Link URL

#### MEDIA
- **Class**: `FigmaMediaNode`
- **Description**: Media content
- **Key Properties**:
  - `mediaData`: Media file data
  - `mediaType`: Media type (IMAGE, VIDEO, AUDIO)

#### TABLE
- **Class**: `FigmaTableNode`
- **Description**: Table structure
- **Contains**: Table cells
- **Key Properties**:
  - `rows`: Number of rows
  - `columns`: Number of columns
  - `cells`: Table cell matrix

#### TABLE_CELL
- **Class**: `FigmaTableCellNode`
- **Description**: Table cell
- **Key Properties**:
  - `rowIndex`: Row position
  - `columnIndex`: Column position  
  - `rowSpan`: Row span
  - `columnSpan`: Column span

#### STICKY
- **Class**: `FigmaStickyNode`
- **Description**: Sticky note
- **Key Properties**:
  - `authorVisible`: Author visibility
  - `backgroundColor`: Background color

#### SHAPE_WITH_TEXT
- **Class**: `FigmaShapeWithTextNode`
- **Description**: Shape with embedded text
- **Contains**: Shape and text components
- **Key Properties**:
  - `textNode`: Text component
  - `shapeNode`: Shape component

#### CONNECTOR
- **Class**: `FigmaConnectorNode`
- **Description**: Connector line between elements
- **Key Properties**:
  - `connectorStart`: Start node ID
  - `connectorEnd`: End node ID
  - `connectorLineSegments`: Line segment points

#### WASHI_TAPE
- **Class**: `FigmaWashiTapeNode`  
- **Description**: Washi tape decoration

#### CODE_BLOCK
- **Class**: `FigmaCodeBlockNode`
- **Description**: Code block element
- **Key Properties**:
  - `code`: Code content
  - `language`: Programming language

#### STAMP
- **Class**: `FigmaStampNode`
- **Description**: Stamp element

## Common Properties

All nodes inherit from `FigmaNode` and include these common properties:

### Basic Properties
- `id`: Unique node identifier
- `name`: Node name  
- `type`: Node type enum
- `visible`: Visibility state
- `locked`: Lock state
- `children`: Child nodes array

### Layout Properties  
- `absoluteBoundingBox`: Absolute position and size
- `absoluteRenderBounds`: Rendered bounds
- `constraints`: Layout constraints
- `layoutPositioning`: Positioning mode (AUTO, ABSOLUTE)
- `relativeTransform`: Transformation matrix

### Visual Properties
- `fills`: Fill paints array
- `strokes`: Stroke paints array  
- `strokeWeight`: Stroke thickness
- `strokeCap`: Stroke end cap style
- `strokeJoin`: Stroke join style
- `strokeDashes`: Dash pattern
- `strokeAlign`: Stroke alignment (INSIDE, OUTSIDE, CENTER)
- `effects`: Visual effects (shadows, blurs)
- `opacity`: Opacity value (0-1)
- `blendMode`: Blend mode
- `isMask`: Whether node is a mask
- `isMaskOutline`: Whether node is mask outline

### Export Properties
- `exportSettings`: Export configurations

### Interaction Properties  
- `reactions`: Interactive reactions
- `transitionNodeID`: Prototype transition target
- `transitionDuration`: Transition duration
- `transitionEasing`: Transition easing

## Usage Examples

### Basic Serialization
```csharp
// Create a node
FigmaRectangleNode rectangle = new FigmaRectangleNode();
rectangle.name = "My Rectangle";
rectangle.cornerRadius = 8f;

// Serialize to JSON
string json = FigmaSerializationUtility.SerializeNode(rectangle);

// Deserialize from JSON  
FigmaNode node = FigmaSerializationUtility.DeserializeNode(json);
```

### Processing API Response
```csharp
// Download from Figma API
string apiResponse = "..."; // JSON from Figma API

// Parse into structured data
FigmaDocument document = FigmaSerializationUtility.ParseFigmaApiResponse(apiResponse);

// Process document
if (document?.document?.children != null)
{
    foreach (var canvas in document.document.children)
    {
        // Process each canvas
    }
}
```

### Node Factory Usage
```csharp
// Parse individual node from JSON
JObject nodeJson = JObject.Parse(jsonString);
FigmaNode node = FigmaNodeFactory.CreateNode(nodeJson);

// The factory automatically creates the correct node type
if (node is FigmaTextNode textNode)
{
    Debug.Log($"Text content: {textNode.characters}");
}
```

## Validation and Testing

Use `FigmaSerializationTester` to validate that all node types are properly serializable:

```csharp
// Test all node types
FigmaSerializationTester tester = GetComponent<FigmaSerializationTester>();
tester.TestAllNodeTypes();
```

This system ensures complete coverage of all Figma node types with proper serialization support for Unity integration.
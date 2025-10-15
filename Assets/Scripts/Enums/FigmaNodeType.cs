using System;

[Serializable]
public enum FigmaNodeType
{
    DOCUMENT,           // Root của file Figma
    CANVAS,             // Canvas chứa frame hoặc group
    FRAME,              // Frame (có thể là artboard)
    GROUP,              // Nhóm các layer
    COMPONENT,          // Component được định nghĩa
    INSTANCE,           // Instance của component
    RECTANGLE,          // Hình chữ nhật
    TEXT,               // Text
    ELLIPSE,            // Hình tròn/ellipse
    LINE,               // Line đơn giản
    VECTOR,             // Vector path (icon, shape)
    STAR,               // Hình sao
    POLYGON,            // Hình đa giác
    BOOLEAN_OPERATION,  // Boolean shape (Union, Subtract, Intersect, Exclude)
    SLICE,              // Vùng slice (dùng xuất image)
    COMPONENT_SET,      // Set chứa nhiều variants (như button variants)

    // Additional types that might be encountered
    REGULAR_POLYGON,    // Regular polygon
    ROUNDED_RECTANGLE,  // Rounded rectangle (variant of rectangle)
    SECTION,            // Section (organizational container)
    WIDGET,             // Widget component
    EMBED,              // Embedded content
    LINK_UNFURL,        // Link preview
    MEDIA,              // Media content
    TABLE,              // Table
    TABLE_CELL,         // Table cell
    STICKY,             // Sticky note
    SHAPE_WITH_TEXT,    // Shape with text
    CONNECTOR,          // Connector line
    WASHI_TAPE,         // Washi tape
    CODE_BLOCK,         // Code block
    STAMP              // Stamp
}
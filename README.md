# Figma to Unity UI Converter - Quick Start

Convert Figma designs directly into Unity UI with TextMeshPro support and automatic prefab creation.

## Quick Setup (30 seconds)

1. **Create GameObject**: Add empty GameObject to scene
2. **Add Script**: Add `FigmaConverterSetup` component
3. **Configure Figma**:
   - Set your Figma API token
   - Set file ID and node ID (or paste full Figma URL)
4. **Set Font**: Assign TextMeshPro font (or use "Setup Default Font" context menu)
5. **Run**: Use "Download and Convert" context menu

## What You Get

✅ **Automatic Canvas Creation** - Creates responsive Canvas with proper scaling  
✅ **TextMeshPro Text** - All text elements use TextMeshPro for crisp rendering  
✅ **Smart Caching** - Downloads once, reuses cached data  
✅ **Prefab Generation** - Auto-saves as prefabs organized by type  
✅ **Layout Preservation** - Maintains Figma positioning and sizing  
✅ **Color Accuracy** - Preserves Figma colors and opacity  

## Supported Figma Elements

| Figma | Unity UI | Notes |
|-------|----------|-------|
| Frame | Container + Image | With background if filled |
| Text | TextMeshPro | Full styling support |
| Rectangle | Image | Solid colors and styling |
| Ellipse | Image | Circular/oval shapes |
| Vector | Image | Icons and custom shapes |
| Group | Container | Grouping element |

## Context Menu Actions

**Right-click on FigmaDownloader component:**
- `Extract File ID from URL` - Auto-extract from Figma URL
- `Download Node Now` - Download fresh data
- `Convert to UI` - Convert to Unity UI
- `Clear Cache` - Force refresh on next download

## File Structure After Conversion

```
Assets/
├── Prefabs/FigmaUI/
│   ├── text/           # Text prefabs  
│   ├── image/          # Image prefabs
│   ├── container/      # Layout prefabs
│   └── Figma_Canvas.prefab
└── Resources/FigmaData/
    └── figma_node_xxx.json  # Cached data
```

## Tips

💡 **Use Node URLs**: Paste full Figma URL instead of manual ID entry  
💡 **Scale Factor**: Adjust scale (0.5-2.0) based on your target resolution  
💡 **Font First**: Set up TextMeshPro font before conversion  
💡 **Cache Smart**: Keep cache for quick iterations, clear when design changes  

## Troubleshooting

❌ **"Component not found"** → Make sure all scripts are compiled  
❌ **"Invalid token"** → Get token from Figma Account Settings  
❌ **"File not found"** → Check if file is shared/accessible  
❌ **Text not showing** → Assign TextMeshPro font asset  

---

**Need more control?** See `FigmaToUI_Documentation.md` for detailed configuration options.
# Sprite Saving System

## Overview

The FigmaConverter now automatically saves generated sprites as persistent Unity assets. This ensures that:
- **Sprites persist after play mode** ends
- **Game objects reference saved sprite assets** instead of runtime-only textures
- **Sprites can be reused** across multiple conversions without regeneration
- **Performance is improved** by loading cached sprites from disk

## How It Works

### 1. Sprite Generation Flow

When converting Figma nodes to Unity UI:

1. **Check Cache**: First checks the in-memory sprite cache
2. **Check Resources**: Then checks if sprite exists in `Assets/Resources/Sprites/{nodeId}/`
3. **Generate**: If not found, generates sprite using `DirectSpriteGenerator`
4. **Save**: Saves the generated sprite as a PNG file
5. **Import**: Configures Unity import settings (Sprite, no mipmaps, etc.)
6. **Load**: Loads the saved sprite and uses it instead of the runtime version
7. **Cache**: Adds to memory cache for faster subsequent access

### 2. File Structure

Generated sprites are saved in:
```
Assets/
  Resources/
    Sprites/
      {nodeId-sanitized}/
        sprite1.png
        sprite2.png
        ...
```

Example:
```
Assets/Resources/Sprites/1234-5678/
  Background.png
  Button.png
  Icon.png
```

### 3. Sprite Naming

- Node names are sanitized to be filesystem-safe
- Special characters are removed or replaced
- Sprites are named after their Figma node names

## Usage

### Converting Figma to Unity

Simply use the existing workflow - sprite saving is automatic:

1. Set your Figma token, file ID, and node ID
2. Click **"Download and Convert Everything"**
3. Sprites are automatically generated and saved
4. Game objects reference the saved sprite assets

### Checking Saved Sprites

Use the new context menu option:

- Right-click the FigmaConverter component
- Select **"Show Sprite Info"**
- View the location and count of saved sprites

### Regenerating Sprites

To force regeneration of all sprites:

1. Delete the sprite folder: `Assets/Resources/Sprites/{nodeId}/`
2. Run **"Download and Convert Everything"** again
3. All sprites will be regenerated and saved

## Key Classes

### SpriteSaveUtility

Located in `Assets/Scripts/Utilities/SpriteSaveUtility.cs`

**Methods:**
- `SaveSpriteToResources(sprite, name, nodeId)` - Saves sprite as PNG asset
- `LoadSpriteFromResources(path)` - Loads saved sprite
- `SpriteExistsInResources(name, nodeId)` - Checks if sprite exists

### UIElementFactory (Updated)

Located in `Assets/Scripts/Rendering/UIElements/UIElementFactory.cs`

**Updated Method:**
- `ApplyStyledSprite()` - Now saves generated sprites automatically

## Benefits

1. **Persistence**: Sprites survive Unity Editor restarts and play mode exits
2. **Performance**: Subsequent conversions load sprites instantly from disk
3. **Asset Management**: Sprites are proper Unity assets that can be managed
4. **Prefabs**: Generated prefabs properly reference sprite assets
5. **Version Control**: Sprite assets can be committed to source control

## Technical Details

### Texture Settings

Saved sprites are configured with:
- **Type**: Sprite (2D and UI)
- **Sprite Mode**: Single
- **Mipmaps**: Disabled
- **Filter Mode**: Bilinear
- **Compression**: Uncompressed
- **Max Size**: 2048

### Editor-Only

Sprite saving only works in Unity Editor (not at runtime). The system falls back to runtime sprites if:
- Running in a build (not editor)
- Save operation fails
- File system is read-only

## Example Log Output

```
Initializing services...
✓ Services already initialized, skipping...
✓ Downloaded: LoginScreen
✓ Indexed 24 nodes
Found 3 nodes to download as images
✓ Saved image: icon.png
Converting node: LoginScreen (Type: FRAME)
Processing: Background (ID: 1234:5678, Type: RECTANGLE)
✓ Saved sprite: Background.png to Assets/Resources/Sprites/1234-5678/
Processing: LoginButton (ID: 1234:5679, Type: FRAME)
✓ Loaded saved sprite: LoginButton.png
...
✓ Figma to UI conversion completed!
✓ Created 15 UI elements
```

## Troubleshooting

**Sprites not saving?**
- Check Unity console for errors
- Ensure `Assets/Resources/` folder exists
- Verify disk space is available
- Check file permissions

**Game objects have missing sprites?**
- Sprites may not have finished importing
- Wait for Unity to finish asset import
- Check the Resources folder for PNG files
- Try running conversion again

**Sprites look wrong?**
- Check the sprite in the Project window
- Verify import settings (should be Sprite type)
- Delete and regenerate if needed


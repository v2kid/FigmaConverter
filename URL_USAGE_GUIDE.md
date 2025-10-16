# Figma URL Input Guide

## ✅ FigmaSimpleConverter with URL Support

The `FigmaSimpleConverter` now supports direct URL input for easy setup!

## 🚀 Quick Start

### 1. **Setup Component**
1. Create a GameObject in your scene
2. Add `FigmaSimpleConverter` component
3. Set your Figma API token

### 2. **Paste Figma URL**
Paste your Figma URL in the `figmaUrl` field:
```
https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15&m=dev
```

### 3. **Extract IDs**
Right-click the component and select **"Extract IDs from URL"**

### 4. **Convert**
Use any of these context menu options:
- **"Download and Convert Everything"** - Downloads all images and converts
- **"Download and Convert (Target Node Only)"** - Downloads only target node images
- **"Convert Without Downloading Images"** - Converts without downloading images

## 📋 Supported URL Formats

The extractor supports these Figma URL formats:

### ✅ **Design URLs**
```
https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15&m=dev
https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001%3A15
```

### ✅ **File URLs**
```
https://www.figma.com/file/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15
https://www.figma.com/file/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001%3A15
```

### ✅ **URLs without node-id**
```
https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled
```
*(Will extract file ID but warn about missing node ID)*

## 🔧 How It Works

### **URL Parsing**
The system automatically extracts:
- **File ID**: `UqdI4flYdmwnwKuQ83EJTF` (from `/design/FILE_ID/` or `/file/FILE_ID/`)
- **Node ID**: `1001:15` (from `?node-id=1001-15` or `?node-id=1001%3A15`)

### **Node ID Conversion**
- Converts `1001-15` → `1001:15`
- Converts `1001%3A15` → `1001:15` (URL decoded)
- Handles both dash and colon formats

### **Auto-Extraction**
All context menu actions automatically extract IDs if they're not already set:
```csharp
// Auto-extract IDs if not already set
if ((string.IsNullOrEmpty(fileId) || fileId == "YOUR_FILE_ID") && !string.IsNullOrEmpty(figmaUrl))
{
    ExtractIdsFromUrl();
}
```

## 🎯 Example Usage

### **Your Specific URL**
```
Input:  https://www.figma.com/design/UqdI4flYdmwnwKuQ83EJTF/Untitled?node-id=1001-15&m=dev
Output: File ID: UqdI4flYdmwnwKuQ83EJTF
        Node ID: 1001:15
```

### **Step-by-Step Process**
1. **Paste URL**: Copy your Figma URL and paste it in the `figmaUrl` field
2. **Extract**: Right-click → "Extract IDs from URL"
3. **Verify**: Check the extracted File ID and Node ID in the inspector
4. **Convert**: Right-click → "Download and Convert Everything"

## 🛠️ Advanced Features

### **Manual Override**
You can still manually set `fileId` and `nodeId` if needed:
- The auto-extraction only runs if IDs are not already set
- Manual values take precedence over URL extraction

### **Validation**
Use **"Validate Setup"** to check:
- ✅ Figma token configuration
- ✅ URL format validation
- ✅ ID extraction test
- ✅ Image download mode

### **Debug Logging**
Enable `enableDebugLogs` to see detailed extraction process:
```
✓ Extracted from URL:
  File ID: UqdI4flYdmwnwKuQ83EJTF
  Node ID: 1001:15
```

## 🚨 Error Handling

### **Common Issues**
- **"Invalid Figma URL"**: URL must contain `figma.com`
- **"Could not extract file ID"**: Check URL format (should have `/design/` or `/file/`)
- **"No node ID found"**: URL doesn't contain `node-id` parameter

### **Troubleshooting**
1. **Check URL Format**: Ensure it's a valid Figma URL
2. **Verify Node Selection**: Make sure you've selected a specific node in Figma
3. **Test Extraction**: Use "Validate Setup" to test URL parsing
4. **Manual Fallback**: Set IDs manually if auto-extraction fails

## 📝 Inspector Layout

```
FigmaSimpleConverter
├── Figma API Settings
│   └── figmaToken: "YOUR_FIGMA_TOKEN"
├── Figma URL Input
│   └── figmaUrl: "https://www.figma.com/design/..."
├── Extracted IDs (Auto-filled from URL)
│   ├── fileId: "UqdI4flYdmwnwKuQ83EJTF"
│   └── nodeId: "1001:15"
├── UI Settings
│   ├── targetCanvas
│   ├── createNewCanvas
│   └── defaultFont
└── Image Download Options
    ├── downloadOnlyTargetNode
    ├── downloadChildrenImages
    └── downloadImages
```

## 🎉 Benefits

- **🚀 Faster Setup**: No more manual ID copying
- **🎯 Accurate**: Automatic extraction prevents typos
- **🔄 Flexible**: Works with both design and file URLs
- **🛡️ Robust**: Handles various URL formats and encodings
- **📱 User-Friendly**: Simple paste-and-go workflow

This makes the Figma to Unity conversion process much more streamlined and user-friendly!

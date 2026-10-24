# 2.1. Document Management in SolidWorks API (C#)

This section covers the lifecycle of SolidWorks files: creating documents from templates, opening existing files with parameters (such as read-only or silent modes), checking the modifications state, and exporting drawings or parts to PDF, DXF, STEP, and IGES.

---

## 🆕 Creating New Documents

To create parts, assemblies, or drawings, use the `NewDocument` method of the `ISldWorks` class. This method requires a path to a template file (`.prtdot`, `.asmdot`, or `.drwdot`).

```csharp
// Get the user's default template path for Parts
string partTemplate = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
IModelDoc2 newPart = swApp.NewDocument(partTemplate, 0, 0, 0) as IModelDoc2;
```

---

## 📂 Opening Documents with Parameters

To open existing files, use `OpenDoc6` or its modern equivalent `IModelDocExtension.OpenDoc7`. These methods let you configure parameters like read-only access, quiet mode (suppressing dialog popups), and configuration selection.

### Open Options Enumeration (`swOpenDocOptions_e`):
*   `swOpenDocOptions_ReadOnly` (1) — Opens the file in read-only mode (safe for shared library components).
*   `swOpenDocOptions_Silent` (2) — Suppresses all popup dialogs (mandatory for unattended automation).
*   `swOpenDocOptions_ViewOnly` (4) — Opens in view-only mode.
*   `swOpenDocOptions_RapidDraft` (8) — Opens drawings in rapid-draft mode.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class DocumentManager
{
    public IModelDoc2 OpenDocumentSafe(ISldWorks swApp, string filePath)
    {
        int fileType = (int)swDocumentTypes_e.swDocNONE;
        
        // Determine file type by extension
        string ext = System.IO.Path.GetExtension(filePath).ToUpper();
        if (ext == ".SLDPRT") fileType = (int)swDocumentTypes_e.swDocPART;
        else if (ext == ".SLDASM") fileType = (int)swDocumentTypes_e.swDocASSEMBLY;
        else if (ext == ".SLDDRW") fileType = (int)swDocumentTypes_e.swDocDRAWING;

        // Combine options: Silent and ReadOnly
        int options = (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly);
        int errors = 0;
        int warnings = 0;

        // Open the document
        IModelDoc2 doc = swApp.OpenDoc6(filePath, fileType, options, "", ref errors, ref warnings);

        if (doc == null)
        {
            Console.WriteLine($"Failed to open {filePath}. Error code: {errors}");
        }
        
        return doc;
    }
}
```

---

## 💾 Saving Documents and Checking for Modifications (Dirty Flag)

Before saving or closing, check if the document was modified by evaluating the "dirty flag" using `GetSaveFlag()`.
*   If `GetSaveFlag()` returns `true`, the document has unsaved changes and requires rebuilding or saving (noted in the GUI with an asterisk `*` next to the document name).

To save, call the `Save3` method:

```csharp
public void SaveDocumentIfDirty(IModelDoc2 model)
{
    // Check for unsaved modifications
    if (model.GetSaveFlag())
    {
        int errors = 0;
        int warnings = 0;
        
        // swSaveAsOptions_Silent (1) - saves without presenting UI prompts
        bool success = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
        
        if (success)
        {
            Console.WriteLine("Document saved successfully.");
        }
        else
        {
            Console.WriteLine($"Save failed. Error code: {errors}");
        }
    }
}
```

---

## 📤 Exporting Documents (PDF, DXF, STEP, IGES)

File exporting is handled through the `ModelDocExtension.SaveAs` method. The output format is automatically determined by the file extension of the target path.

### 1. Exporting Drawings to PDF
```csharp
public bool ExportToPdf(IModelDoc2 model, string outputPdfPath)
{
    IModelDocExtension ext = model.Extension;
    int errors = 0;
    int warnings = 0;

    // Optional: Configure PDF Export Settings (e.g. exporting all sheets or active sheet only)
    ExportPdfData pdfData = swApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as ExportPdfData;
    
    // Pass pdfData to SaveAs
    bool success = ext.SaveAs(outputPdfPath, 
        (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
        (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
        pdfData, 
        ref errors, 
        ref warnings);
        
    return success;
}
```

### 2. Exporting Sheet Metal Flat Patterns to DXF
For sheet metal parts (`.SLDPRT`), you often need to export flat patterns to DXF. Use `ExportToDWG2` on the `IPartDoc` interface.

```csharp
public void ExportFlatPatternToDxf(IPartDoc part, string outputDxfPath, string flatPatternFeatureName)
{
    IModelDoc2 model = (IModelDoc2)part;
    
    // Parameters for flat pattern DWG/DXF export
    // 1 - Sheet metal flat pattern view
    int alignment = 0; // Alignment orientation
    double[] varAlignment = new double[12]; // Alignment matrix coordinates
    
    bool success = part.ExportToDWG2(
        outputDxfPath, 
        model.GetPathName(), 
        1, // swExportToDWG_e.swExportFlatPatternView
        true, // Use sheet metal options
        varAlignment, 
        false, 
        false, 
        0, 
        null);
        
    if (!success)
    {
        Console.WriteLine("Failed to export flat pattern to DXF.");
    }
}
```

### 3. Exporting 3D Models to STEP / IGES
```csharp
public bool ExportToStep(IModelDoc2 model, string outputStepPath)
{
    IModelDocExtension ext = model.Extension;
    int errors = 0;
    int warnings = 0;

    bool success = ext.SaveAs(outputStepPath,
        (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
        (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
        null, // Global STEP preferences will be used automatically
        ref errors,
        ref warnings);
        
    return success;
}
```

---

## 🛠 Document Lifecycle Control Methods

| Method | Class | Description |
| :--- | :--- | :--- |
| `CloseDoc(Name)` | `ISldWorks` | Closes the specified open document by its title. |
| `CloseAllDocuments(bool)` | `ISldWorks` | Closes all open documents. Pass `true` to discard unsaved changes silently. |
| `ForceRebuild3(bool)` | `IModelDoc2` | Rebuilds the geometry or drawing. The `topOnly` argument rebuilds only the top-level assembly if `true`. |
| `GetPathName()` | `IModelDoc2` | Returns the absolute path of the file on disk. Returns an empty string if the file is new and has not been saved yet. |

---

## ⚠️ Automation Precautions

> [!CAUTION]
> When closing files via `CloseDoc` or `CloseAllDocuments`, manage your C# COM references carefully.
> If you retain a document reference variable in C# after closing that document inside SolidWorks, invoking any method on it will throw a `COMException` (disconnected object). Always assign variables to `null` immediately after calling `Marshal.ReleaseComObject(doc)`.

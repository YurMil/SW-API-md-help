# 5.6. Advanced Sheets and Templates in SolidWorks API (C#)

This section details advanced drawing sheet management: programmatically configuring sheets (`SetupSheet6`), changing templates, adding or removing sheets, and implementing auto-scaling layout algorithms to ensure views fit perfectly within sheet boundaries.

---

## 📄 Custom Sheet Configuration (SetupSheet6)

The `SetupSheet6` method allows you to configure drawing sheets, including sizes, custom sheet format template paths (`.slddrt`), projection directions (First vs Third Angle), and zone formatting properties.

### Paper Size Options (`swDwgPaperSizes_e`):
*   `swDwgPaperA4size` (0) — Standard A4 paper.
*   `swDwgPaperA3size` (1) — Standard A3 paper.
*   `swDwgPapersUserDefined` (12) — Custom paper dimensions defined by width/height properties (meters).

---

## 💻 C# Code: Dynamic Sheet Setup and Format Reloading

The following code reconfigures an active drawing sheet to an A3 format, loads a custom border template, sets a 1:2 default scale, and activates Third-Angle projection.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class SheetConfigurator
{
    public void ReconfigureSheet(IDrawingDoc drawing, string sheetName, string slddrtPath)
    {
        IModelDoc2 model = (IModelDoc2)drawing;

        // SetupSheet6 parameters:
        // Name - Target sheet name
        // PaperSize - swDwgPaperSizes_e
        // TemplateIn - swDwgTemplates_e (custom template vs default templates)
        // Scale1 / Scale2 - Default sheet scale (e.g. 1:2 scale)
        // FirstAngle - Projection system (true = First Angle / European, false = Third Angle / American)
        // TemplateName - Path to the custom .slddrt sheet format file
        // Width / Height - Custom paper dimensions (in meters, used when PaperSize = 12)
        // PropertyViewName - Default view name to inherit custom properties from
        // RemoveModifiedNotes - true clears old sheet format notes during template swap
        bool success = drawing.SetupSheet6(
            sheetName,
            (int)swDwgPaperSizes_e.swDwgPaperA3size, 
            (int)swDwgTemplates_e.swDwgTemplateCustom, 
            1, 2, // Scale 1:2
            false, // Third Angle projection
            slddrtPath, 
            0.420, 0.297, // A3 Dimensions: 420mm x 297mm
            "Default", 
            true, 
            0, 0, 0, 0, // Zone margins (left, right, top, bottom)
            0, 0        // Zone rows, columns
        );

        if (success)
        {
            Console.WriteLine($"Sheet '{sheetName}' successfully updated.");
            
            // Force rebuild to redraw sheet formats
            model.ForceRebuild3(true);
        }
    }

    public void AddNewSheet(IDrawingDoc drawing, string newSheetName, string slddrtPath)
    {
        IModelDoc2 model = (IModelDoc2)drawing;

        // Creates a new sheet in the drawing
        // Parameters: Name, PaperSize, TemplateIn, Scale1, Scale2, FirstAngle, TemplateName, Width, Height, PropertyViewName
        bool success = drawing.NewSheet(
            newSheetName,
            (int)swDwgPaperSizes_e.swDwgPaperA4size,
            (int)swDwgTemplates_e.swDwgTemplateCustom,
            1, 1,
            true,
            slddrtPath,
            0.297, 0.210,
            "Default"
        );

        if (success)
        {
            Console.WriteLine($"New sheet '{newSheetName}' added.");
            model.ForceRebuild3(true);
        }
    }
}
```

---

## 📐 Drawing View Auto-Scaling Layout Algorithm

When placing views programmatically, they might overflow the page boundaries if the model is large. Below is a C# layout algorithm that calculates the total bounding box of drawing views and adjusts the sheet scale factor to fit all views inside the sheet margin.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class LayoutAutoScaler
{
    public void AutoScaleSheetToFitViews(IDrawingDoc drawing, string sheetName)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // 1. Get sheet dimensions (Width and Height in meters)
        ISheet sheet = drawing.GetCurrentSheet() as ISheet;
        if (sheet == null) return;
        
        double[] sheetProps = (double[])sheet.GetProperties2();
        double sheetWidth = sheetProps[5]; // Width in meters (e.g. 0.420 for A3)
        double sheetHeight = sheetProps[6]; // Height in meters (e.g. 0.297 for A3)

        // Define a 20mm margin around sheet boundaries (in meters)
        double margin = 0.02;
        double availableWidth = sheetWidth - (margin * 2.0);
        double availableHeight = sheetHeight - (margin * 2.0);

        // 2. Traverse views to find the collective boundary limits of all projected geometry
        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;

        IView sheetView = drawing.GetFirstView() as IView;
        IView currentView = sheetView?.GetNextView() as IView; // Skip the Sheet View node

        int viewCount = 0;

        while (currentView != null)
        {
            // Get view center location (meters)
            double[] viewPos = (double[])currentView.Position;
            double vx = viewPos[0];
            double vy = viewPos[1];

            // Get view bounding box size (returns: [MinX, MinY, MaxX, MaxY] in sheet space meters)
            double[] viewOutline = (double[])currentView.GetOutline();
            
            if (viewOutline != null)
            {
                minX = Math.Min(minX, viewOutline[0]);
                minY = Math.Min(minY, viewOutline[1]);
                maxX = Math.Max(maxX, viewOutline[2]);
                maxY = Math.Max(maxY, viewOutline[3]);
                viewCount++;
            }

            currentView = currentView.GetNextView() as IView;
        }

        if (viewCount == 0) return; // No views to scale

        // Calculate total bounding box width and height occupied by the views
        double occupiedWidth = maxX - minX;
        double occupiedHeight = maxY - minY;

        // 3. Compute required scaling factors
        double scaleFactorX = availableWidth / occupiedWidth;
        double scaleFactorY = availableHeight / occupiedHeight;
        
        // Use the smaller scaling factor to ensure fitting in both directions
        double finalScaleFactor = Math.Min(scaleFactorX, scaleFactorY);

        if (finalScaleFactor < 1.0) // If views exceed the page bounds
        {
            // Calculate a clean denominator (e.g. if factor is 0.43, scale is 1:2.3 -> round up to 1:3 or 1:5)
            double currentScaleDenominator = sheetProps[3];
            double newDenominator = Math.Ceiling(currentScaleDenominator / finalScaleFactor);

            Console.WriteLine($"Views occupied area ({occupiedWidth*1000:F1}x{occupiedHeight*1000:F1} mm) exceeds sheet.");
            Console.WriteLine($"Adjusting sheet scale denominator from {currentScaleDenominator} to {newDenominator}.");

            // Apply new scale via SetupSheet6
            string slddrtPath = sheet.GetTemplateName();
            drawing.SetupSheet6(
                sheetName,
                (int)sheetProps[0], // PaperSize
                (int)sheetProps[1], // TemplateIn
                (int)sheetProps[2], // Scale1 (numerator, typically 1)
                (int)newDenominator, // Scale2 (new denominator)
                sheetProps[4] == 1, // FirstAngle projection flag
                slddrtPath,
                sheetWidth,
                sheetHeight,
                "Default",
                false,
                0, 0, 0, 0, 0, 0
            );

            model.ForceRebuild3(true);
        }
        else
        {
            Console.WriteLine("All views fit within sheet boundaries. Scaling not required.");
        }
    }
}
```

---

## 🛠 Sheets & Templates Methods Reference

| Method | Class | Description |
| :--- | :--- | :--- |
| `SetupSheet6(...)` | `IDrawingDoc` | Configures sheet options, scales, borders, and zone parameters. |
| `NewSheet(...)` | `IDrawingDoc` | Creates and adds a new drawing sheet. |
| `DeleteSheet(Name)` | `IDrawingDoc` | Removes a sheet by name. |
| `GetTemplateName()` | `ISheet` | Returns the file path to the `.slddrt` sheet format template active on the sheet. |
| `GetOutline()` | `IView` | Returns the outer box dimensions `[MinX, MinY, MaxX, MaxY]` of the view. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Obsolete SetupSheet5 Methods:**
    Do not use `SetupSheet5` or older sheet configuration wrappers. They do not support drawing border zones and sheet grid tracking introduced in modern SolidWorks versions, which can lead to layout corruption.
2.  **Deleting Sheets Context:**
    When deleting sheets via `DeleteSheet()`, make sure you are not trying to delete the last remaining sheet in the drawing. SolidWorks drawings require at least one active sheet to exist. Deleting the last sheet will fail.
3.  **Round-Up Scales:**
    The auto-scaling algorithm computes raw floating-point denominators. For professional drawings, round these denominators to standard engineering scales (e.g. `2`, `5`, `10`, `20`, `50`, `100`), rather than using decimal scales like `1:2.34`.

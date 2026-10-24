# 5.7. Advanced BOM & Balloon Customization in SolidWorks API (C#)

This section covers advanced Bill of Materials (BOM) customization (sorting, cell resizing, splitting), advanced balloon formatting, using magnetic lines (`IMagneticLine`) for callout alignment, and auto-arranging dimensions to avoid overlapping texts.

---

## 📊 Programmatic BOM Table Sorting

To sort BOM tables programmatically, obtain a `BomTableSortData` object from the table annotation, define primary and secondary column sort indexes, and apply the sort.

### C# Code: Sorting and Formatting BOM Cells

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class AdvancedBomFormatter
{
    public void SortAndFormatBom(IDrawingDoc drawing, IBomTableAnnotation bomTable)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        ITableAnnotation table = bomTable as ITableAnnotation;

        if (table == null) return;

        // --- PART 1: Sorting the BOM Table ---
        // Disable "Follow assembly order" in table properties to allow custom sorting
        bomTable.FollowAssemblyOrder = false;

        // Retrieve sorting parameters container
        IBomTableSortData sortData = bomTable.GetBomTableSortData();
        
        // Define Primary Sort: Column index 2 (e.g. BOMIDNumber), Ascending
        sortData.set_ColumnIndex(0, 2); 
        sortData.set_Ascending(0, true);

        // Define Secondary Sort: Column index 3 (e.g. BOMDescription), Ascending
        sortData.set_ColumnIndex(1, 3);
        sortData.set_Ascending(1, true);

        // Save sorting rules to the table
        sortData.SaveCurrentSortParameters = true;

        // Apply sorting parameters
        bool sortSuccess = bomTable.Sort(sortData);
        Console.WriteLine($"BOM table sorted: {sortSuccess}");

        // --- PART 2: Formatting Cell Sizes & Alignments ---
        // Freeze table refresh during updates for performance
        int totalRows = table.RowCount;
        int totalCols = table.ColumnCount;

        // Set column widths (in meters)
        table.SetColumnWidth(0, 0.015); // Item Number column: 15mm
        table.SetColumnWidth(1, 0.015); // Qty column: 15mm
        table.SetColumnWidth(2, 0.040); // Part Number column: 40mm
        table.SetColumnWidth(3, 0.080); // Description column: 80mm

        // Format cell text justifications and alignments
        for (int r = 0; r < totalRows; r++)
        {
            table.SetRowHeight(r, 0.008); // Row height: 8mm
            
            for (int c = 0; c < totalCols; c++)
            {
                // Set text alignments: 2 = Center (swTextAlignment_e)
                table.set_TextAlignment(r, c, (int)swTextAlignment_e.swTextAlignmentCenter);
            }
        }

        model.ForceRebuild3(false);
    }
}
```

---

## 🎈 Advanced Balloon Styling and Magnetic Lines (IMagneticLine)

When adding callouts, you can customize the balloon borders, define split balloon formats (e.g. displaying Item Number on top and Quantity on bottom), and snap callouts to **Magnetic Lines** to keep the layout organized.

### C# Code: Creating Magnetic Lines and Custom Balloons

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class BalloonStyleManager
{
    public void CreateAlignedBalloons(IDrawingDoc drawing, string viewName)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // 1. Select the drawing view
        model.ClearSelection2(true);
        drawing.ActivateView(viewName);
        model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);

        ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
        IView view = selMgr.GetSelectedObject6(1, -1) as IView;

        if (view != null)
        {
            // 2. Add a magnetic line inside the drawing view coordinates (in meters)
            // Parameters: StartX, StartY, EndX, EndY
            // Place horizontal line at Y=180mm from X=50mm to X=200mm
            IMagneticLine magneticLine = view.AddMagneticLine(0.05, 0.18, 0.2, 0.18) as IMagneticLine;

            if (magneticLine != null)
            {
                // Align callouts horizontally (0 = horizontal, 1 = vertical)
                magneticLine.EqualSpacing = true;

                // 3. Configure and add a custom balloon to an edge
                // We pre-select an edge on a component
                model.ClearSelection2(true);
                model.Extension.SelectByID2("", "EDGE", 0.08, 0.1, 0.0, false, 0, null, 0);

                BalloonOptions opt = model.Extension.CreateBalloonOptions();
                opt.Style = (int)swBalloonStyle_e.swBS_SplitCircular; // Split circle border style
                opt.Size = (int)swBalloonFit_e.swBF_Tightest;
                opt.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber; // Item number on top
                opt.LowerTextContent = (int)swBalloonTextContent_e.swBalloonTextQuantity;   // Component Qty on bottom
                opt.ShowQuantity = true;

                // Insert balloon
                INote note = model.Extension.InsertBOMBalloon2(opt) as INote;

                if (note != null)
                {
                    // 4. Attach the balloon annotation to the magnetic line
                    IAnnotation annotation = note.GetAnnotation() as IAnnotation;
                    magneticLine.AddAnnotation(annotation);
                    System.Diagnostics.Debug.WriteLine("Balloon attached to Magnetic Line.");
                }
            }
        }

        model.ClearSelection2(true);
        model.ForceRebuild3(false);
    }
}
```

---

## 📐 Auto-Arranging Dimensions (AlignDimensions)

To prevent manual dimensions from overlapping each other or overlapping model edges, select the dimensions and invoke the `AlignDimensions` helper.

```csharp
public void AutoArrangeDimensions(IModelDoc2 model, string viewName)
{
    IDrawingDoc drawing = model as IDrawingDoc;
    if (drawing == null) return;

    model.ClearSelection2(true);
    drawing.ActivateView(viewName);

    // 1. Traverse annotations in the view and select all display dimensions
    IView sheetView = drawing.GetFirstView() as IView;
    IView currentView = sheetView?.GetNextView() as IView;

    while (currentView != null)
    {
        if (currentView.GetName2().Equals(viewName, StringComparison.OrdinalIgnoreCase))
        {
            // Traverse annotations
            IAnnotation ann = currentView.GetFirstAnnotation2() as IAnnotation;
            while (ann != null)
            {
                // If type is 5 = swAnnotationType_e.swDisplayDimension
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    // Select with accumulation (Append = true)
                    ann.Select2(true, 0);
                }
                ann = ann.GetNext3() as IAnnotation;
            }
            break;
        }
        currentView = currentView.GetNextView() as IView;
    }

    // 2. Apply auto-arrange alignment algorithm
    // Offset parameter (0.0015 = 1.5mm distance gap spacing)
    bool success = model.Extension.AlignDimensions(
        (int)swAlignDimensionType_e.swAlignDimensionType_AutoArrange, 
        0.0015
    );

    if (success)
    {
        Console.WriteLine("Dimensions aligned and arranged successfully.");
    }

    model.ClearSelection2(true);
    model.ForceRebuild3(false);
}
```

---

## 🛠 Advanced BOM & Balloon Options Reference

### Balloon Style Options (`swBalloonStyle_e`):
*   `swBS_None` (0) — No border.
*   `swBS_Circular` (1) — Standard circle outline.
*   `swBS_SplitCircular` (2) — Circle split horizontally in half.
*   `swBS_Box` (3) — Square/rectangular border.
*   `swBS_Triangle` (5) — Triangle border.
*   `swBS_Hexagon` (6) — Hexagon border.

### Key API Methods:

| Method | Interface | Description |
| :--- | :--- | :--- |
| `GetBomTableSortData()` | `IBomTableAnnotation`| Retrieves sorting parameters for the BOM. |
| `Sort(SortData)` | `IBomTableAnnotation`| Sorts the BOM table using the specified parameters. |
| `SetColumnWidth(Col, Width)`| `ITableAnnotation` | Sets column width (meters). |
| `SetRowHeight(Row, Height)` | `ITableAnnotation` | Sets row height (meters). |
| `AddMagneticLine(X1,Y1,X2,Y2)`| `IView` | Creates a magnetic line to align balloon callouts. |
| `AlignDimensions(AlignType,Gap)`| `IModelDocExtension`| Automatically arranges selected dimensions on drawings. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Sorting Errors with Linked Assembly Orders:**
    If `IBomTableAnnotation.Sort()` fails, verify that `FollowAssemblyOrder` is set to `false`. If it is `true`, SolidWorks locks sorting configurations to follow the assembly tree sequence, causing the `Sort` call to fail.
2.  **Magnetic Line Coordinates:**
    Magnetic lines must reside inside the drawing view boundary limits. Placing coordinates outside view outlines will prevent balloons from snapping to the line.
3.  **Dimension Pre-Selection:**
    The `AlignDimensions` method works strictly on the active selection. If you call it when no dimensions are selected, it returns `false` without applying changes.

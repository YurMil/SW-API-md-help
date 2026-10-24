# 5.2. Annotations and BOM in SolidWorks using C#

Drawing detailing involves placing Bill of Materials (BOM) tables and matching identification balloons. This guide covers the programmatic placement of auto-balloons and mapping drawing table columns directly to 3D model properties.

---

## 📊 Managing Bill of Materials (BOM) Tables

A BOM table annotation is represented by the `IBomTableAnnotation` interface, which inherits from the base `ITableAnnotation` class.

### Locating BOM Tables and Mapping Columns
BOM tables are identified by their type code `swTableAnnotationType_e.swTableAnnotation_BillOfMaterials` (value `2`). Once located, you can link table columns to custom model properties:

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class BomManager
{
    public void ConfigureBomColumns(IDrawingDoc drawing)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // Traverse drawing views to locate tables
        IView view = drawing.GetFirstView() as IView;
        while (view != null)
        {
            ITableAnnotation table = view.GetFirstTableAnnotation() as ITableAnnotation;
            while (table != null)
            {
                if (table.Type == (int)swTableAnnotationType_e.swTableAnnotation_BillOfMaterials)
                {
                    IBomTableAnnotation bomTable = table as IBomTableAnnotation;
                    
                    // Bind columns to custom properties
                    for (int col = 0; col < table.ColumnCount; col++)
                    {
                        string headerText = table.GetColumnTitle2(col);

                        // Match headers to custom properties
                        if (headerText.ToUpper() == "PART NO." || headerText.ToUpper() == "IDNUMBER")
                        {
                            bomTable.SetColumnCustomProperty(col, "BOMIDNumber");
                        }
                        else if (headerText.ToUpper() == "DESCRIPTION")
                        {
                            bomTable.SetColumnCustomProperty(col, "BOMDescription");
                        }
                        else if (headerText.ToUpper() == "MATERIAL")
                        {
                            bomTable.SetColumnCustomProperty(col, "Material");
                        }
                        else if (headerText.ToUpper() == "MASS")
                        {
                            bomTable.SetColumnCustomProperty(col, "Mass");
                        }
                    }
                    
                    Console.WriteLine("BOM column mappings configured.");
                }
                table = table.GetNextTableAnnotation() as ITableAnnotation;
            }
            view = view.GetNextView() as IView;
        }
        
        model.ForceRebuild3(false);
    }
}
```

---

## 🎈 Automatic Balloon Placement (AutoBalloon5)

To place BOM index balloons automatically on drawing views, use the `AutoBalloon5` method.

> [!IMPORTANT]
> **View-Level Traversal Required:**
> Once `AutoBalloon5` runs, the new balloon annotations may not appear in the document-level annotations collection (`IModelDoc2.GetFirstAnnotation2()`).
> You must query and filter note annotations at the **individual view level** using `IView.GetAnnotations()`.

### Auto-Balloon Placement Code:

```csharp
public void AutoBalloonView(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;

    // 1. Select the drawing view
    model.ClearSelection2(true);
    bool selected = model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);

    if (!selected) return;

    // 2. Configure AutoBalloon Options
    AutoBalloonOptions options = drawing.CreateAutoBalloonOptions();
    options.Layout = (int)swDetailingBalloonLayout_e.swDetailingBalloonLayout_Circle; // Circular layout
    options.IgnoreMultiple = true; // Avoid placing multiple balloons for duplicate parts
    options.LeaderAttachmentToFaces = true; // Attach leader arrow to face geometry
    options.Style = (int)swBalloonStyle_e.swBS_Circular; // Circular balloons
    options.Size = (int)swBalloonFit_e.swBF_Tightest; // Compact scaling
    options.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber; // Retrieve BOM item index
    options.LowerTextContent = (int)swBalloonTextContent_e.swBalloonTextCustom;
    options.ItemOrder = (int)swBalloonItemNumbers_e.swBalloonItemNumbers_DoNotChangeItemNumbers; // Preserves BOM sequence
    options.InsertMagneticLine = true; // Attach to magnetic layout lines

    // 3. Execute placement
    object[] result = (object[])drawing.AutoBalloon5(options);
    
    if (result != null)
    {
        Console.WriteLine($"Balloons placed on view '{viewName}': {result.Length}");
    }
    
    model.ClearSelection2(true);
}
```

---

## 🗑 Deleting Duplicate Balloon Annotations

SolidWorks auto-balloons often place multiple callouts for identical components. The following routine scans drawing views and deletes duplicate index callouts.

```csharp
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class BalloonCleaner
{
    public void DeleteDuplicateBalloons(IDrawingDoc drawing)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        HashSet<string> seenPositions = new HashSet<string>();
        List<IAnnotation> duplicateAnnotations = new List<IAnnotation>();

        // 1. Traverse all sheets and views
        IView sheetView = drawing.GetFirstView() as IView;
        IView currentView = sheetView?.GetNextView() as IView;

        while (currentView != null)
        {
            object[] annotations = (object[])currentView.GetAnnotations();
            
            if (annotations != null)
            {
                foreach (object annObj in annotations)
                {
                    IAnnotation annotation = annObj as IAnnotation;
                    if (annotation == null) continue;

                    // Filter note annotations: type 6 = swAnnotationType_e.swNote
                    if (annotation.GetType() == (int)swAnnotationType_e.swNote)
                    {
                        INote note = annotation.GetSpecificAnnotation() as INote;
                        
                        // Check if the note is a BOM balloon callout
                        if (note != null && note.IsBomBalloon())
                        {
                            string itemNumber = note.GetText(); // Retrieve index number (e.g. "5")

                            if (seenPositions.Contains(itemNumber))
                            {
                                // Mark duplicate for deletion
                                duplicateAnnotations.Add(annotation);
                            }
                            else
                            {
                                seenPositions.Add(itemNumber);
                            }
                        }
                    }
                }
            }
            
            currentView = currentView.GetNextView() as IView;
        }

        // 2. Select and delete marked duplicates
        if (duplicateAnnotations.Count > 0)
        {
            model.ClearSelection2(true);
            
            foreach (IAnnotation ann in duplicateAnnotations)
            {
                ann.Select2(true, 0); // Append selections (Append = true)
            }

            // Delete selections
            model.Extension.DeleteSelection2(0);
            model.ClearSelection2(true);
            
            Console.WriteLine($"Deleted duplicate balloons: {duplicateAnnotations.Count}");
            model.ForceRebuild3(false);
        }
    }
}
```

---

## 🛠 Annotation Reference Table

| Method | Class | Description |
| :--- | :--- | :--- |
| `IsBomBalloon()` | `INote` | Evaluates if the drawing note is a BOM balloon callout. |
| `GetText()` | `INote` / `IAnnotation` | Reads the text string displayed inside the callout or note. |
| `GetFirstTableAnnotation()`| `IView` | Returns the first table annotation on the designated drawing view. |
| `SetColumnCustomProperty(...)`| `IBomTableAnnotation`| Links a column field index to a custom property string. |
| `DisplayedText2[R, C, B]` | `ITableAnnotation` | Reads the final evaluated cell text string for row R and column C. |

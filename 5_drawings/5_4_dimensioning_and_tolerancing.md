# 5.4. Dimensioning and Tolerancing in SolidWorks API (C#)

This section covers programmatically placing dimensions (standard, ordinate, and coordinate layouts), importing annotations directly from 3D models, editing dimension text properties, configuring tolerances, and placing drawing notes.

---

## 📥 Importing Model Dimensions (InsertModelAnnotations3)

Instead of manually drawing dimensions, the recommended practice is to import dimensions directly from the 3D part or assembly features using the `InsertModelAnnotations3` method.

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class AnnotationImporter
{
    public void ImportDimensionsFromModel(IDrawingDoc drawing)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // Configure options for importing annotations
        // Import all types of dimensions, including driving, reference, and tolerances
        int types = (int)(swImportAnnotationTypes_e.swImportAnnotations_AllDimensions);
        int options = (int)(swImportModelAnnotationsOptions_e.swImportModelAnnotations_ImportToAllViews |
                            swImportModelAnnotationsOptions_e.swImportModelAnnotations_IncludeHiddenFeatures);

        // Execute import on active sheet
        drawing.InsertModelAnnotations3(
            (int)swModelItemsSource_e.swModelItemsFromEntireModel, // Source
            types, 
            options, 
            true // Auto-arrange imported dimensions
        );

        model.ForceRebuild3(false);
    }
}
```

---

## 💻 C# Code: Creating Manual Dimensions

If you need to define manual dimensions (e.g. check gaps or clearance tolerances), select the target geometry entities (faces or edges) and invoke selection-based dimensioning.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class DimensionManager
{
    public void AddManualLinearDimension(IDrawingDoc drawing, string viewName)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // 1. Activate the target drawing view
        drawing.ActivateView(viewName);
        model.ClearSelection2(true);

        // 2. Select two parallel edges in the view context to dimension the gap
        // (Coordinates are in meters relative to sheet space)
        bool edge1Selected = model.Extension.SelectByID2("", "EDGE", 0.08, 0.12, 0.0, true, 0, null, 0);
        bool edge2Selected = model.Extension.SelectByID2("", "EDGE", 0.12, 0.12, 0.0, true, 0, null, 0);

        if (edge1Selected && edge2Selected)
        {
            // 3. Insert dimension (X, Y, Z coordinates locate the text placement on sheet)
            IDisplayDimension displayDim = model.AddDimension2(0.1, 0.14, 0.0) as IDisplayDimension;
            
            if (displayDim != null)
            {
                // 4. Access the underlying Dimension object
                IDimension dimension = displayDim.GetDimension2(0);
                
                // Add custom suffix text
                displayDim.TextAfter = " REF";
                
                // Configure Bilateral Tolerances
                // Value: 1 = swTolType_e.swTolBILATERAL
                dimension.Tolerance.Type = (int)swTolType_e.swTolBILATERAL;
                dimension.Tolerance.SetValues(0.0005, -0.0002); // +0.5mm / -0.2mm in meters
                
                Console.WriteLine("Manual dimension with tolerances successfully added.");
            }
        }

        model.ClearSelection2(true);
        model.ForceRebuild3(false);
    }
}
```

---

## 📐 Creating Ordinate Dimension Layouts

Ordinate dimensions are highly preferred in CNC manufacturing. They define all locations relative to a single zero baseline.

```csharp
public void AddOrdinateDimensionChain(IModelDoc2 model, List<IEntity> edgeEntities, double textX, double textY)
{
    model.ClearSelection2(true);

    if (edgeEntities.Count == 0) return;

    // 1. Select the base datum entity (which will represent 0.0)
    edgeEntities[0].Select4(false, null);

    // 2. Insert the baseline ordinate dimension (zero marker)
    // AddOrdinateDimension parameters: type (0=horizontal, 1=vertical), location X, Y, Z
    model.AddOrdinateDimension(0, textX, textY, 0.0);

    // 3. Add subsequent ordinate lines along the chain
    for (int i = 1; i < edgeEntities.Count; i++)
    {
        model.ClearSelection2(true);
        edgeEntities[i].Select4(false, null);
        
        // Add dimension pointing to the established baseline chain
        model.AddOrdinateDimension(0, textX, textY, 0.0);
    }

    model.ClearSelection2(true);
    model.ForceRebuild3(false);
}
```

---

## 📝 Placing Drawing Notes (CreateNewNote3)

Drawing notes (annotations) are created using the `CreateNewNote3` method of the `IModelDoc2` interface.

```csharp
public void PlaceGeneralNote(IModelDoc2 model, string viewName, string text, double x, double y)
{
    // If placing note inside a view, ensure it is activated
    IDrawingDoc drawing = model as IDrawingDoc;
    drawing?.ActivateView(viewName);

    // Create the note
    INote note = model.CreateNewNote3(
        text, 
        x, y, 0.0, // Coordinate placement (meters)
        (int)swCreateNoteOptions_e.swCreateNoteOptions_NoLeader, // Option: no leader line
        0, // Angle (radians)
        out _
    ) as INote;

    if (note != null)
    {
        // Customize text format properties
        note.SetTextJustification((int)swTextJustification_e.swTextJustificationCenter);
        Console.WriteLine("Drawing Note inserted.");
    }
}
```

---

## 🛠 Dimension and Tolerance Options Reference

### Tolerance Type Enumeration (`swTolType_e`):
*   `swTolNONE` (0) — No tolerance.
*   `swTolBILATERAL` (1) — Bilateral tolerance (differing plus and minus values).
*   `swTolSYMMETRIC` (2) — Symmetric tolerance (same plus/minus value).
*   `swTolLIMIT` (3) — Display limit values directly.
*   `swTolFIT` (4) — Standard fit designations (e.g. H7/g6).

---

## ⚠️ Known Issues and Developer Rules

1.  **Tolerance Values in Meters:**
    Like all CAD parameters, tolerance limit offsets set via `SetValues()` must be defined in **meters**. A value of `0.05` represents a tolerance of +50mm. Use scientific double notations (e.g. `0.001` for 1mm).
2.  **Activating Views Before Dimensioning:**
    Always call `drawing.ActivateView(viewName)` before adding notes or manual dimensions. Adding dimensions while the root "Sheet View" is active associates the annotations to the sheet page rather than the drawing view. If the view is moved by the user, sheet-associated dimensions will fail to follow and become detached.
3.  **Dimension Text Wrapping:**
    For multi-line notes or long suffix annotations, control text widths using `INote.Width` and `INote.Height` parameters to prevent overlapping text lines.

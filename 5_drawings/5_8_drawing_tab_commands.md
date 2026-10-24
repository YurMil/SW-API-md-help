# 5.8. CommandManager Drawing Tab Reference in SolidWorks API (C#)

This reference maps every visual command on the **Drawing Tab** of the SOLIDWORKS CommandManager to its corresponding C# API interface, methods, and enumerations.

---

## 🗺 CommandManager Drawing Tab API Mapping

| CommandManager Button | SOLIDWORKS API Interface | Primary Method / Property | Description |
| :--- | :--- | :--- | :--- |
| **Model View** | `IDrawingDoc` | `CreateDrawViewFromModelView3` | Inserts a base view from a named orientation. |
| **Projected View** | `IDrawingDoc` | `CreateProjectedView2` | Generates projection views relative to a selected view. |
| **Auxiliary View** | `IDrawingDoc` | `CreateAuxiliaryViewAt2` | Generates auxiliary views from a selected edge. |
| **Section View** | `IDrawingDoc` | `CreateSectionViewAt5` / `6` | Creates section cuts based on a selected sketch line. |
| **Detail View** | `IDrawingDoc` | `CreateDetailViewAt4` | Creates enlarged detail bubbles from a sketch circle. |
| **Standard 3 View** | `IDrawingDoc` | `Create3rdAngleViews2` | Automatically inserts Front, Top, and Right views. |
| **Broken-out Section** | `IDrawingDoc` | `CreateBreakOutSection` | Creates a partial depth cut using a closed sketch loop. |
| **Break View** | `IView` | `InsertBreak3` | Breaks a long view using vertical or horizontal gaps. |
| **Crop View** | `IView` | `Crop2` | Restricts view borders using a selected sketch loop. |
| **Alternate Position** | `IView` | `CreateAlternatePositionView` | Overlays a secondary assembly configuration view. |
| **Empty View** | `IDrawingDoc` | `CreateEmptyViewAt` | Places an empty sketch boundary box onto the sheet. |
| **Replace Model** | `IView` | `ReplaceModelWith` | Replaces the referenced model file for a drawing view. |

---

## 💻 C# Code Templates for Advanced View Commands

### 1. Broken-out Section View (CreateBreakOutSection)
Requires a drawing view to be active and a closed sketch profile (e.g. circle or spline) selected on that view.

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public void CreateBrokenOutSection(IDrawingDoc drawing, string parentViewName, double depthMeters)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    
    // 1. Activate the target view
    drawing.ActivateView(parentViewName);
    model.ClearSelection2(true);

    // 2. Sketch a closed profile (e.g. a circle)
    ISketchManager sketchMgr = model.SketchManager;
    sketchMgr.AddToDB = true;
    ISketchSegment boundary = sketchMgr.CreateCircleByRadius(0.12, 0.15, 0.0, 0.025);
    sketchMgr.AddToDB = false;

    if (boundary != null)
    {
        // 3. Select the sketch profile boundary
        IEntity boundaryEntity = (IEntity)boundary;
        boundaryEntity.Select4(false, null);

        // 4. Generate the Broken-out Section cut
        bool success = drawing.CreateBreakOutSection(depthMeters);
        
        if (success)
        {
            System.Diagnostics.Debug.WriteLine("Broken-out section cut created successfully.");
        }
    }
}
```

### 2. Break View (InsertBreak3)
Splits a long view with vertical/horizontal lines. After adding break lines, call `BreakView` to render the break gaps.

```csharp
public void AddHorizontalBreakToView(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    
    model.ClearSelection2(true);
    model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
    ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
    IView view = selMgr.GetSelectedObject6(1, -1) as IView;

    if (view != null)
    {
        // InsertBreak3 parameters:
        // Orientation: 0 = Horizontal, 1 = Vertical (swBreakLineOrientation_e)
        // Position1 / Position2: cut locations (meters, relative to view origin)
        // Style: 0 = Straight, 1 = Jagged (swBreakLineStyle_e)
        // ShapeIntensity: 1 to 5 (for jagged lines)
        // BreakSketchBlocks: false
        view.InsertBreak3(
            (int)swBreakLineOrientation_e.swBreakLineOrientation_Horizontal,
            -0.03, // Position 1 (30mm below view origin)
            0.02,  // Position 2 (20mm above view origin)
            (int)swBreakLineStyle_e.swBreakLineStyle_Jagged,
            3,
            false
        );

        // Finalize and recalculate the break views layout on sheet
        drawing.BreakView();
        model.ForceRebuild3(true);
    }
}
```

### 3. Crop View (Crop2)
Clips the view outline to a closed sketch boundary. Select the closed sketch segment before calling the method.

```csharp
public void CropDrawingView(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    drawing.ActivateView(viewName);

    // 1. Draw the crop outline (e.g. rectangle)
    ISketchManager sketchMgr = model.SketchManager;
    sketchMgr.AddToDB = true;
    object[] rect = (object[])sketchMgr.CreateCornerRectangle(0.08, 0.18, 0.0, 0.14, 0.12, 0.0);
    sketchMgr.AddToDB = false;

    if (rect != null && rect.Length > 0)
    {
        // 2. Select the sketch segment
        ISketchSegment firstSegment = rect[0] as ISketchSegment;
        
        // Select the sketch entity
        ISketch sketch = firstSegment.GetSketch() as ISketch;
        IEntity sketchEntity = (IEntity)sketch;
        model.ClearSelection2(true);
        sketchEntity.Select4(false, null);

        // Get view interface
        model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
        IView view = selMgr.GetSelectedObject6(1, -1) as IView;

        // 3. Apply the crop
        // Crop2 parameters: JaggedOutline, NoOutline, ShapeIntensity
        int status = view.Crop2(false, false, 0);

        if (status == 0) // swCropViewErrors_e.swCropView_Success
        {
            Console.WriteLine("View cropped successfully.");
        }
    }
}
```

### 4. Replace Referenced Model (ReplaceModelWith)
Swaps out the underlying 3D model component for a drawing view, updating all dimensions and geometry links.

```csharp
public void ReplaceViewModel(IView view, string newModelPath)
{
    // Replaces the referenced document for the drawing view
    // Note: The new document must be of the same type (Part/Assembly) and contain matching feature names
    bool success = view.ReplaceModelWith(newModelPath);
    
    if (success)
    {
        Console.WriteLine("Referenced model replaced successfully.");
    }
}
```

---

## ⚠️ Important Developer Rules

1.  **Broken-out Section Depth Reference:**
    The depth input for `CreateBreakOutSection` is relative to the model's orientation. You can use coordinate point selections on components to automatically obtain depth values.
2.  **Crop View Sketch Restrictions:**
    A drawing view can only host a single Crop operation. You cannot crop a view that already has a crop or a detail view attached to it.
3.  **Break View Alignment Coordinates:**
    The coordinates passed to `InsertBreak3` must be defined **relative to the drawing view origin**, not the sheet origin. Verify view center positions using `IView.Position` before calculating coordinates.

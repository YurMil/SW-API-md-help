# 5.10. Sketching in Drawings using SolidWorks API (C#)

This section covers programmatically drawing sketch elements, applying sketch relations, modifying geometry (trim, offset, mirror, patterns), and dimensioning sketch entities inside drawing sheets or drawing views.

---

## 📐 Drawing Scope: Sheet vs. View Context

Drawing sketch entities reside in either sheet space or view space. You must explicitly set the active scope using `ActivateView`.

*   **Sheet Context:** Drawing directly on the sheet (e.g. template borders, title blocks).
    ```csharp
    // Set focus to the root sheet view (empty string name)
    drawing.ActivateView("");
    ```
*   **View Context:** Drawing inside a specific model projection view. Sketch coordinates will map to the view's local 2D layout origin.
    ```csharp
    drawing.ActivateView("Drawing View1");
    ```

---

## 💻 C# Code: Creating Sketch Primitives

When drawing programmatically, access the `ISketchManager` from the active document. All coordinate inputs must be specified in **meters**.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class DrawingSketcher
{
    public void DrawPrimitivesInView(IDrawingDoc drawing, string viewName)
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        
        // 1. Activate the target view
        drawing.ActivateView(viewName);
        ISketchManager sketchMgr = model.SketchManager;

        // Force sketch to write directly to document database for speed
        sketchMgr.AddToDB = true;

        // 2. Draw a Line: X1, Y1, Z1, X2, Y2, Z2 (in meters)
        ISketchLine line = sketchMgr.CreateLine(0.01, 0.01, 0.0, 0.05, 0.01, 0.0) as ISketchLine;

        // 3. Draw a Circle: CenterX, CenterY, CenterZ, Radius
        ISketchSegment circle = sketchMgr.CreateCircleByRadius(0.08, 0.08, 0.0, 0.015);

        // 4. Draw a Rectangle: Bottom-Left X, Y, Z, Top-Right X, Y, Z
        // Note: returns array of segments representing the 4 sides
        object[] rect = (object[])sketchMgr.CreateCornerRectangle(0.12, 0.01, 0.0, 0.16, 0.04, 0.0);

        sketchMgr.AddToDB = false;
        model.ForceRebuild3(false);
    }
}
```

---

## 🛠 Sketch Modifiers (Trim, Offset, Mirror, Patterns)

Below are the primary methods for modifying sketch entities in drawing sketches.

```csharp
public void ApplyOffsetAndMirror(IModelDoc2 model, ISketchSegment lineSegment, ISketchLine centerlineForMirror)
{
    ISketchManager sketchMgr = model.SketchManager;
    model.ClearSelection2(true);

    // 1. Offset Entities
    // Select the segment to offset
    IEntity lineEntity = (IEntity)lineSegment;
    lineEntity.Select4(false, null);

    // Offset parameters: Distance (meters), Flip, SelectChain, DoubleSided, CapType, MakeConstruction
    // Create a 5mm offset line
    bool offsetSuccess = sketchMgr.SketchOffset2(0.005, false, false, false, 0, false);
    
    // 2. Mirror Entities
    model.ClearSelection2(true);
    // Select the geometry to mirror
    lineEntity.Select4(true, null);
    // Select the centerline axis (Mirror reference)
    IEntity axisEntity = (IEntity)centerlineForMirror;
    axisEntity.Select4(true, null);

    // Mirror geometry relative to centerline selection
    sketchMgr.SketchMirror();
}

public void CreateLinearPattern(IModelDoc2 model, ISketchSegment circleSegment)
{
    ISketchManager sketchMgr = model.SketchManager;
    model.ClearSelection2(true);
    
    IEntity circleEntity = (IEntity)circleSegment;
    circleEntity.Select4(false, null);

    // Linear pattern parameters:
    // NumberX, SpacingX, NumberY, SpacingY, AngleX, AngleY, ...
    // Create 3 instances spaced 25mm apart along X axis
    bool patternSuccess = sketchMgr.CreateLinearSketchPattern(
        3, 0.025, // X axis: 3 instances, 25mm spacing
        1, 0.0,   // Y axis: 1 instance
        0.0, 0.0, // Angles (radians)
        true, false, false, false, true
    );
}
```

---

## 📐 Adding Dimensions to Sketch Entities (AddSpecificDimension)

To place dimensions on sketch segments, select the entities via `SelectByID2` and call `AddSpecificDimension` using the appropriate `swDimensionType_e` enum.

```csharp
public void DimensionSketchElements(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    drawing.ActivateView(viewName);
    
    IModelDocExtension ext = model.Extension;
    int error = 0;

    // --- Linear Dimensioning (Length of line) ---
    model.ClearSelection2(true);
    // Select a line segment (provide coordinates intersecting the segment)
    ext.SelectByID2("", "SKETCHSEGMENT", 0.03, 0.01, 0.0, false, 0, null, 0);
    
    // Insert horizontal dimension placed at Y=20mm
    ext.AddSpecificDimension(
        0.03, 0.02, 0.0, 
        (int)swDimensionType_e.swHorLinearDimension, 
        out error
    );

    // --- Angular Dimensioning (Angle between two lines) ---
    model.ClearSelection2(true);
    // Select line 1
    ext.SelectByID2("", "SKETCHSEGMENT", 0.03, 0.01, 0.0, true, 0, null, 0);
    // Select line 2
    ext.SelectByID2("", "SKETCHSEGMENT", 0.08, 0.05, 0.0, true, 0, null, 0);

    // Insert angular dimension at target text coordinate location
    ext.AddSpecificDimension(
        0.05, 0.03, 0.0, 
        (int)swDimensionType_e.swAngularDimension, 
        out error
    );
}
```

---

## 🔗 Display / Delete Sketch Relations

Relations inside drawing sketches are managed via the `ISketchRelationManager` interface.

```csharp
public void DeleteRelationsInActiveSketch(IModelDoc2 model)
{
    ISketchActiveStatus sketchStatus = model.SketchContext as ISketchActiveStatus;
    
    // Ensure we are editing a sketch
    if (sketchStatus != null && sketchStatus.Active)
    {
        ISketch activeSketch = sketchStatus.Sketch;
        ISketchRelationManager relMgr = activeSketch.RelationManager;

        // Retrieve all relations (swAll = 0)
        object[] relations = (object[])relMgr.GetRelations((int)swSketchRelationFilterType_e.swAll);

        if (relations != null)
        {
            foreach (object obj in relations)
            {
                ISketchRelation rel = obj as ISketchRelation;
                if (rel != null)
                {
                    // Delete the relation
                    rel.Delete();
                }
            }
            Console.WriteLine($"Removed {relations.Length} sketch relations.");
        }
    }
}
```

---

## 🛠 CommandManager Sketch Tab API Reference

### Dimension Type Options (`swDimensionType_e`):
*   `swHorLinearDimension` (1) — Horizontal linear dimension.
*   `swVertLinearDimension` (2) — Vertical linear dimension.
*   `swRadialDimension` (3) — Radial arc/circle dimension.
*   `swDiameterDimension` (4) — Diameter circle dimension.
*   `swAngularDimension` (5) — Angular dimension between selected entities.

### Key API Methods:

| Method | Interface | Description |
| :--- | :--- | :--- |
| `CreateLine(x1,y1,z1,x2,y2,z2)` | `ISketchManager` | Draws a sketch line. |
| `CreateCircleByRadius(x,y,z,r)` | `ISketchManager` | Draws a sketch circle. |
| `SketchOffset2(dist,flip,...)` | `ISketchManager` | Offsets selected sketch segments. |
| `SketchMirror()` | `ISketchManager` | Mirrors selected segments across a centerline. |
| `AddSpecificDimension(...)` | `IModelDocExtension`| Places a specific dimension type at coordinates. |
| `RelationManager` | `ISketch` | Property that returns the sketch relations manager. |

---

## ⚠️ Important Developer Rules

1.  **Selection Precondition for Trimming/Offsetting:**
    Trimming or mirroring operations act strictly on the active selected entities. Make sure `Select4()` returns `true` before calling modifiers.
2.  **Sketch coordinates vs. Sheet coordinates:**
    When a drawing view is activated, drawing coordinates mapped via `SketchManager` are relative to the **view origin**, not the sheet bottom-left. Be careful when drawing layout boundaries.
3.  **Clean selections after sketch creation:**
    Always clear selections (`model.ClearSelection2(true)`) after drawing new sketch segments, otherwise subsequent dimensioning calls will pick up the segments and error.

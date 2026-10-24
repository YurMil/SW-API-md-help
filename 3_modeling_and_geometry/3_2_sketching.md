# 3.2. Sketching in SolidWorks using C#

Creating sketches is the foundation for building 3D geometry in SolidWorks. The `ISketchManager` interface controls entering/exiting sketch modes and drawing geometric primitives.

---

## 🛠 Standard Sketching Workflow

1.  Select a plane (e.g., `"Front Plane"`, `"Top Plane"`, `"Right Plane"`) or a flat face on the active model.
2.  Call `InsertSketch(true)` to enter sketch editing mode.
3.  Draw geometric primitives (lines, circles, rectangles) using `ISketchManager` methods.
4.  (Optional) Add sketch constraints (Relations) and dimensions.
5.  Call `InsertSketch(true)` again to exit sketch mode and save changes.

---

## 💻 2D Sketch Creation Example in C#

Below is a C# example that selects the Front Plane, enters sketch mode, draws a corner rectangle, places a circle at the origin, and applies a parametric diameter dimension.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class SketchCreator
{
    public void CreatePartSketch(IModelDoc2 model)
    {
        ISketchManager sketchMgr = model.SketchManager;
        if (sketchMgr == null) return;

        // 1. Select the Front Plane for the sketch
        // Note: For localized SolidWorks versions (e.g. Russian), the plane name is "Спереди"
        bool planeSelected = model.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
        if (!planeSelected)
        {
            // Fallback for Russian localized name
            model.Extension.SelectByID2("Спереди", "PLANE", 0, 0, 0, false, 0, null, 0);
        }

        // 2. Enter sketch editing mode
        // InsertSketch(true) opens a new sketch on the selected plane
        sketchMgr.InsertSketch(true);

        // Turn off automatic dimension dialog prompts during creation
        model.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);

        // 3. Draw a rectangle by opposite corners (coordinates are in METERS)
        // Returns an array of created sketch segments
        object[] rectSegments = (object[])sketchMgr.CreateCornerRectangle(-0.05, 0.05, 0, 0.05, -0.05, 0);

        // 4. Draw a circle at the origin (X=0, Y=0, Z=0) with radius R=0.02 meters (20 mm)
        // Returns an ISketchSegment (which can be cast to ISketchArc)
        ISketchSegment circle = sketchMgr.CreateCircleByRadius(0, 0, 0, 0.02);

        // 5. Add a diameter dimension to the circle
        if (circle != null)
        {
            // Select the circle to apply the dimension
            circle.Select4(false, null);
            
            // Add dimension (X, Y, Z locate the text position in meters)
            IDisplayDimension dim = model.AddDimension2(0.04, 0.04, 0);
            if (dim != null)
            {
                // Retrieve the underlying dimension object and set its value in meters
                IDimension dimension = dim.GetDimension2(0);
                dimension.SystemValue = 0.035; // Change diameter value to 35 mm
            }
        }

        // 6. Exit sketch mode and save changes
        sketchMgr.InsertSketch(true);
    }
}
```

---

## 🌌 Working with 3D Sketches

3D sketches are not constrained to a single flat plane and allow drawing lines and splines in 3D space (useful for piping, structural frames, and routing).

Methods used for 3D sketching:
*   `Insert3DSketch(bool Update)` — Enters or exits a 3D sketch.
*   Lines are drawn using `CreateLine(x1, y1, z1, x2, y2, z2)`.

```csharp
public void Create3DSketch(IModelDoc2 model)
{
    ISketchManager sketchMgr = model.SketchManager;
    
    // Enter 3D Sketch mode
    sketchMgr.Insert3DSketch(true);
    
    // Draw connected lines in 3D space (coordinates in meters)
    sketchMgr.CreateLine(0, 0, 0, 0.1, 0, 0);       // From (0,0,0) to (100,0,0) mm
    sketchMgr.CreateLine(0.1, 0, 0, 0.1, 0.1, 0);    // Up by 100 mm along Y
    sketchMgr.CreateLine(0.1, 0.1, 0, 0.1, 0.1, 0.1); // Forward by 100 mm along Z
    
    // Exit 3D Sketch mode
    sketchMgr.Insert3DSketch(true);
}
```

---

## 🛠 Core Geometric Methods in ISketchManager

| Method | Return Type | Description |
| :--- | :--- | :--- |
| `CreateLine(x1, y1, z1, x2, y2, z2)` | `ISketchLine` | Draws a line segment between two coordinates. |
| `CreateCircleByRadius(xc, yc, zc, r)` | `ISketchSegment` | Draws a circle around a center point with a specified radius. |
| `CreateCornerRectangle(x1, y1, z1, x2, y2, z2)` | `object[]` | Draws a rectangle using two corner coordinates. |
| `CreateCenterRectangle(xc, yc, zc, x, y, z)` | `object[]` | Draws a rectangle from a center point to a corner coordinate. |
| `CreateFillet(Seg1, x1, y1, z1, Seg2, x2, y2, z2, R)`| `ISketchSegment` | Creates a fillet corner between two sketch segments. |
| `InsertSketch(bool Update)` | `void` | Enters or exits (saving changes) the active sketch. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Plane Name Localization:**
    Standard planes are named `"Front Plane"`, `"Top Plane"`, and `"Right Plane"` in English, but are named `"Спереди"`, `"Сверху"`, and `"Справа"` in Russian. Check for both names or parse tree features dynamically to avoid failure.
2.  **Sketch Left Open (Ghost State):**
    If a sketch is not visible or editable after your program finishes, verify that you called `InsertSketch(true)` a second time to save and exit the sketch mode.
3.  **System Value Units:**
    When modifying dimension values directly using `dimension.SystemValue`, always use SI units: **meters** for lengths, and **radians** for angles, regardless of the active document's display units setting.

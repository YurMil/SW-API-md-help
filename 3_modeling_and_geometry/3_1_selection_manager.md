# 3.1. Selection Manager in SolidWorks using C#

Most interactive operations in SolidWorks (creating fillets, mates, sketches, cuts) require choosing geometric entities first. Programmatic selection is handled using the `ISelectionMgr` interface.

---

## 🔍 Getting the Selection Manager and Inspecting Active Selections

Get the `ISelectionMgr` interface directly from the active document:

```csharp
ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
```

### Core methods for inspecting selected items:
*   `GetSelectedObjectCount2(int Sheet)` — Returns the count of selected entities. Passing `-1` searches the entire document.
*   `GetSelectedObjectType3(int Index, int Sheet)` — Returns the type code of the selected entity (returns value from `swSelectType_e` enum).
*   `GetSelectedObject6(int Index, int Sheet)` — Returns the selected entity as a generic `object`, which you can then cast to `IFace2`, `IEdge`, `IComponent2`, etc.

### Iterating through selected entities in C#:

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class SelectionInspector
{
    public void InspectSelection(IModelDoc2 model)
    {
        ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
        if (selMgr == null) return;

        int count = selMgr.GetSelectedObjectCount2(-1);
        Console.WriteLine($"Selected objects count: {count}");

        for (int i = 1; i <= count; i++)
        {
            int type = selMgr.GetSelectedObjectType3(i, -1);
            object selectedObj = selMgr.GetSelectedObject6(i, -1);

            Console.WriteLine($"Entity #{i}: Type = {(swSelectType_e)type}");

            if (type == (int)swSelectType_e.swSelFACES)
            {
                IFace2 face = selectedObj as IFace2;
                Console.WriteLine($"Face selected. Area: {face.GetArea()} sq. meters.");
            }
        }
    }
}
```

---

## 🎯 Programmatic Selection via SelectByID2

To select items programmatically, use `SelectByID2` on the `ModelDocExtension` interface. This supports selecting items by name from the design tree, or by 3D coordinates on the model's geometry.

```csharp
bool success = model.Extension.SelectByID2(
    string Name, 
    string Type, 
    double X, double Y, double Z, 
    bool Append, 
    int Mark, 
    object Callout, 
    int SelectOption
);
```

### Parameters breakdown:
1.  **`Name`**: Unique name of the entity in the FeatureManager design tree (e.g., `"Front Plane"`, `"Sketch1"`, `"Cut-Extrude1"`). When selecting by coordinates, pass an empty string `""`.
2.  **`Type`**: Entity type as a string. Common types:
    *   `"PLANE"` — Datum planes.
    *   `"SKETCH"` — Sketches.
    *   `"BODYFEATURE"` — Modeling features (bosses, cuts).
    *   `"COMPONENT"` — Assembly components.
    *   `"FACE"`, `"EDGE"`, `"VERTEX"` — Faces, edges, vertices.
    *   `"DRAWINGVIEW"` — Drawing views.
3.  **`X`, `Y`, `Z`**: 3D coordinates in meters (used when choosing a face/edge by its location in 3D space). Pass `0, 0, 0` when selecting strictly by name.
4.  **`Append`**: 
    *   `false` — Clears the previous selection before choosing the item.
    *   `true` — Adds the item to the active selection (behaves like holding the Ctrl key).
5.  **`Mark`**: Selection mark. A critical parameter for features that take multiple distinct inputs (see below). Pass `0` if not needed.
6.  **`Callout`**: Pointer to a callout object. Typically passed as `null` or `0`.
7.  **`SelectOption`**: Selection options. Typically passed as `0`.

### Selection Examples:

```csharp
// Select "Front Plane" to start a sketch (clearing previous selection)
model.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);

// Add an assembly component to the active selection (Append = true)
model.Extension.SelectByID2("Bolt-1@Assembly", "COMPONENT", 0, 0, 0, true, 0, null, 0);

// Clear selection
model.ClearSelection2(true);
```

---

## 🏷 Understanding Selection Marks

Many feature creation methods require inputs to be selected with specific "Marks" so that SolidWorks can distinguish between different input roles.

**Example (Creating a Loft feature):**
When creating a Loft, you must select multiple sketch profiles. If you also want to use a Guide Curve, it must be selected with a different mark, otherwise SolidWorks will treat it as a loft profile.

*   Profile sketches are selected with a mark of `1` (`Mark = 1`).
*   Guide curves are selected with a mark of `2` (`Mark = 2`).

```csharp
// Select first profile (Mark = 1)
model.Extension.SelectByID2("Sketch1", "SKETCH", 0, 0, 0, false, 1, null, 0);

// Select second profile (Mark = 1, Append = true)
model.Extension.SelectByID2("Sketch2", "SKETCH", 0, 0, 0, true, 1, null, 0);

// Select guide curve sketch (Mark = 2, Append = true)
model.Extension.SelectByID2("GuideSketch", "SKETCH", 0, 0, 0, true, 2, null, 0);

// Invoke Loft feature creation via FeatureManager...
```

---

## ⚠️ Common Pitfalls

1.  **Coordinate Units in Millimeters:**
    All coordinates in the SolidWorks API (including `SelectByID2`) are measured strictly in **meters**. If you pass `X = 50` targeting a point on a 50mm wide part, the selection will fail because it targets a point 50 meters away. Divide your values by `1000.0` to convert millimeters to meters.
2.  **Stale Active Selections:**
    Before executing critical operations (like adding mates or inserting auto-balloons), call `model.ClearSelection2(true)`. Leftover selections from previous steps can cause features to fail or act on the wrong entities.
3.  **Assembly Component Names:**
    Assembly components include instance suffix annotations (e.g. `"Bolt-1@Assembly"`, not simply `"Bolt"`). Obtain the exact component name for selection using the `IComponent2.Name2` property.

# 3.3. Features in SolidWorks using C#

A SolidWorks part document is composed of a sequential tree of design features — extrusions, cuts, revolves, fillets, and chamfers. Creating and editing these features programmatically is managed via the `IFeatureManager` interface.

---

## 🛠 Feature Creation Workflow

1.  Select the sketch (or geometry) to use for the feature.
2.  Set up the feature parameters (direction, depth, draft angles, etc.).
3.  Invoke the appropriate creation method on the `IFeatureManager` object.
4.  Force a document rebuild to update the model geometry (`model.ForceRebuild3(false)`).

---

## 💻 C# Boss Extrusion and Cut Example

The following code selects a sketch, extrudes it into a solid body, selects a second sketch, and performs a through-all cut.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class FeatureCreator
{
    public void ExtrudeAndCut(IModelDoc2 model)
    {
        IFeatureManager featMgr = model.FeatureManager;
        if (featMgr == null) return;

        // --- STEP 1: Extruding a Boss Feature ---
        // 1. Select the sketch by name
        model.ClearSelection2(true);
        bool sketchSelected = model.Extension.SelectByID2("Sketch1", "SKETCH", 0, 0, 0, false, 0, null, 0);

        if (sketchSelected)
        {
            // 2. Create the boss extrusion
            // FeatureExtrusion3 Parameters:
            // sd (Single Direction) = true
            // Flip = false (do not reverse direction)
            // Dir = false (extrude outward from plane)
            // T1 (Type 1 condition) = swEndConditions_e.swEndCondBlind
            // Depth = 0.05 (50 mm depth)
            IFeature extrudeFeat = featMgr.FeatureExtrusion3(
                true, false, false, 
                (int)swEndConditions_e.swEndCondBlind, 0, 
                0.05, 0.05, // Depth for Direction 1 and Direction 2
                false, false, false, false, 
                0, 0, false, false, false, false, 
                true, true, true, 
                (int)swStartConditions_e.swStartSketchPlane, 0, false
            );

            if (extrudeFeat != null)
            {
                extrudeFeat.Name = "Base_Extrusion"; // Rename the feature in the tree
                Console.WriteLine("Boss extrusion successfully created.");
            }
        }

        // --- STEP 2: Creating a Cut Extrusion ---
        // Select sketch "Sketch2" containing the cut profile
        model.ClearSelection2(true);
        bool cutSketchSelected = model.Extension.SelectByID2("Sketch2", "SKETCH", 0, 0, 0, false, 0, null, 0);

        if (cutSketchSelected)
        {
            // Create a through-all extrusion cut
            // T1 = swEndConditions_e.swEndCondThroughAll
            IFeature cutFeat = featMgr.FeatureCut4(
                true, false, false, 
                (int)swEndConditions_e.swEndCondThroughAll, 0, 
                0.01, 0.01, // Depth (ignored for Through All)
                false, false, false, false, 
                0, 0, false, false, false, false, 
                false, true, true, true, true, 
                (int)swStartConditions_e.swStartSketchPlane, 0, false, false
            );

            if (cutFeat != null)
            {
                cutFeat.Name = "Through_Cut";
                Console.WriteLine("Extrusion cut successfully created.");
            }
        }

        // Force a rebuild of the model
        model.ForceRebuild3(false);
    }
}
```

---

## 🔄 Modifying Existing Feature Parameters (Feature Data)

To change properties of an existing feature (such as changing the depth of an extrusion or a fillet radius), extract the feature's definition properties (Feature Data).

Workflow for modifying feature definitions:
1.  Find the target feature by name or traversal.
2.  Extract its parameter definition object (e.g. `IExtrudeFeatureData2` for extrusions or `ISimpleFilletFeatureData2` for fillets) using `IFeature.GetDefinition()`.
3.  Open the definition for editing in the API using `AccessSelections()`.
4.  Modify the parameters (e.g., using `SetDepth()` or assigning values).
5.  Commit changes by calling `IFeature.ModifyDefinition()`.
6.  Release selection locks using `ReleaseSelectionAccess()`.

```csharp
public void ModifyExtrusionDepth(IModelDoc2 model, string featureName, double newDepthMeters)
{
    // Retrieve the feature by name
    IFeature feature = model.FeatureByName(featureName) as IFeature;
    if (feature == null) return;

    // Get the feature definition data
    IExtrudeFeatureData2 extrudeData = feature.GetDefinition() as IExtrudeFeatureData2;
    if (extrudeData == null) return;

    // Lock selections for modifying the definition
    extrudeData.AccessSelections(model, null);

    // Set the new depth for Direction 1 (true = Direction 1)
    extrudeData.SetDepth(true, newDepthMeters);

    // Apply the updated definition
    feature.ModifyDefinition(extrudeData, model, null);

    // Release the selection access lock
    extrudeData.ReleaseSelectionAccess();
    
    // Rebuild the model
    model.ForceRebuild3(false);
}
```

---

## 🛠 FeatureManager Creation Methods

| Method | Return Type | Description |
| :--- | :--- | :--- |
| `FeatureExtrusion3(...)` | `IFeature` | Extrudes a boss or base feature from a selected sketch. |
| `FeatureCut4(...)` | `IFeature` | Cuts solid material out using a selected sketch profile. |
| `FeatureRevolve2(...)` | `IFeature` | Revolves a sketch profile around a center axis. |
| `FeatureFillet3(...)` | `IFeature` | Creates fillets along selected edges or faces. |
| `FeatureChamfer(...)` | `IFeature` | Creates chamfers along selected edges or faces. |
| `InsertWeldmentFeature()` | `IFeature` | Converts the part into a Weldment, enabling cut-list folder groupings. |

---

## ⚠️ Known Issues and Developer Rules

1.  **COM Exceptions on ModifyDefinition:**
    If you modify a feature's parameter data, you must call `AccessSelections()` before making modifications and `ReleaseSelectionAccess()` afterwards. Skipping `AccessSelections()` causes SolidWorks to throw a COM exception during property edits.
2.  **Forgotten Rebuild Call:**
    Changes made via Feature Data properties will not update in the graphical display immediately. Call `model.ForceRebuild3(false)` once your parameter changes are committed.
3.  **Deleting Features:**
    To delete a feature, select it and call `model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed)`.

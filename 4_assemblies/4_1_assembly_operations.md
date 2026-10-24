# 4.1. Assembly Operations in SolidWorks using C#

A SolidWorks assembly (`.SLDASM`) integrates parts and subassemblies into a hierarchical structure. Common automation tasks include adding components to the assembly, positioning them in 3D space using transformation matrices, and applying mechanical mates.

To execute assembly-specific commands, cast the base `IModelDoc2` interface to `IAssemblyDoc`.

---

## ➕ Adding Components to an Assembly

To insert an existing part or assembly into the active assembly document, use the `AddComponent5` method.

```csharp
IComponent2 AddComponent5(
    string CompName, 
    int ConfigOption, 
    string ConfigName, 
    bool LogicalCopy, 
    string TargetConfig, 
    double X, double Y, double Z
);
```

*   `CompName` — Absolute path to the part or assembly file on disk.
*   `ConfigOption` — Configuration selection choice (`swAddComponentConfigOptions_e`). Using `swAddComponentConfigOptions_e.swAddComponentConfigOptions_TargetSelected` (1) lets you specify a target configuration.
*   `ConfigName` — Name of the target configuration in the component to insert (e.g. `"Default"`).
*   `LogicalCopy` — Typically passed as `false`.
*   `TargetConfig` — Name of the configuration in the parent assembly where the component is inserted (an empty string `""` inserts it into all configurations).
*   `X`, `Y`, `Z` — Coordinates in meters relative to the assembly origin.

---

## 📐 Positioning Components via MathTransform

After adding a component, you can change its position. In the SolidWorks API, component positions are defined by a 3D transformation matrix `IMathTransform` (representing rotation and translation).

Create transformation objects using `IMathUtility`, which is retrieved via `ISldWorks.GetMathUtility()`.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class AssemblyManager
{
    public void AddAndPositionComponent(ISldWorks swApp, IAssemblyDoc assemblyDoc, string partPath)
    {
        // 1. Insert component at the origin (0, 0, 0)
        IComponent2 comp = assemblyDoc.AddComponent5(
            partPath, 
            (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelected, 
            "", false, "", 0, 0, 0
        );

        if (comp == null)
        {
            Console.WriteLine("Failed to insert component into the assembly.");
            return;
        }

        // 2. Define a transform: shift by 100 mm along X and 50 mm along Y
        IMathUtility mathUtil = swApp.GetMathUtility() as IMathUtility;
        
        // Define a 16-element transformation array (identity rotation matrix + translation vector)
        // Values are in meters
        double[] transformData = new double[16] {
            1.0, 0.0, 0.0, // X-axis orientation vector
            0.0, 1.0, 0.0, // Y-axis orientation vector
            0.0, 0.0, 1.0, // Z-axis orientation vector
            0.1, 0.05, 0.0, // Translation vector: X = 100mm, Y = 50mm, Z = 0mm
            1.0,           // Scaling factor
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0 // Reserved values
        };

        IMathTransform transform = mathUtil.CreateTransform(transformData) as IMathTransform;

        // Apply the transform to the component
        comp.Transform2 = transform;

        // Rebuild the assembly doc
        IModelDoc2 model = (IModelDoc2)assemblyDoc;
        model.ForceRebuild3(false);
    }
}
```

---

## 🔗 Creating Assembly Mates

Applying mechanical assembly mates (coincident, concentric, parallel) is a two-step process:
1.  Select the mating entities (faces, edges, planes) of the components using `SelectByID2`.
2.  Invoke the `AddMate5` method on the `IAssemblyDoc` interface.

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public void CreateCoincidentMate(IModelDoc2 model, IAssemblyDoc assembly)
{
    // Clear active selections
    model.ClearSelection2(true);

    // 1. Select the mating face on Component 1 (Append = true, Mark = 1)
    // Assembly selection name format: "FaceName@InstanceName@AssemblyName"
    // Use 3D coordinates on the face when selecting without a name string
    bool face1Selected = model.Extension.SelectByID2(
        "", "FACE", 0.05, 0.02, 0.0, 
        true, 1, null, 0
    );

    // 2. Select the mating face on Component 2 (Append = true, Mark = 1)
    bool face2Selected = model.Extension.SelectByID2(
        "", "FACE", 0.15, 0.02, 0.0, 
        true, 1, null, 0
    );

    if (face1Selected && face2Selected)
    {
        int mateType = (int)swMateType_e.swMateCOINCIDENT; // Coincident
        int align = (int)swMateAlign_e.swMateAlignALIGNED; // Aligned orientation
        int errors = 0;

        // Create the mate
        IMate2 mate = assembly.AddMate5(
            mateType, 
            align, 
            false, // Flip (do not reverse orientation)
            0,     // Distance (if applicable)
            0,     // Angle (if applicable)
            0, 0,  // Distance tolerances
            0, 0,  // Angle tolerances
            0, 0,  // Special mate parameters
            false, // Allow positioning
            false, // Use for positioning only
            out errors
        ) as IMate2;

        if (mate != null)
        {
            System.Diagnostics.Debug.WriteLine("Coincident mate successfully created.");
        }
        
        // Clear active selections
        model.ClearSelection2(true);
        model.ForceRebuild3(false);
    }
}
```

---

## 🛠 Mate Types Enumeration (`swMateType_e`)

| Constant | Integer Value | Description |
| :--- | :--- | :--- |
| `swMateCOINCIDENT` | **0** | Coincident |
| `swMateCONCENTRIC` | **1** | Concentric (coaxial) |
| `swMatePERPENDICULAR`| **2** | Perpendicular |
| `swMatePARALLEL` | **3** | Parallel |
| `swMateTANGENT` | **4** | Tangent |
| `swMateDISTANCE` | **5** | Distance (requires offset value) |
| `swMateANGLE` | **6** | Angle (requires angular value) |

---

## ⚠️ Common Assembly Issues

1.  **Mating Fixed Components:**
    The first component inserted into an assembly is locked in place (`Fixed`) by default. Attempting to add mates that move this component will fail. You can make it float programmatically using `comp.MakeFloating(true)`.
2.  **Assembly Context in Selections:**
    Mates must be created using entity references selected *within the context of the assembly*. Selecting a face from a separately opened part file and passing it to assembly mate creation will cause an error. Selection must happen via `SelectByID2` using the component path inside the active assembly.
3.  **MathTransform Arrays:**
    The array passed to `CreateTransform` must contain exactly 16 double values. The first 9 elements must represent normalized direction vectors for the X, Y, and Z axes. Invalid coordinates or values will distort the components.

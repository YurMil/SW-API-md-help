# 9.3. Entities and Selection in SolidWorks API (C#)

In SolidWorks, almost all topological elements (faces, edges, vertices, coordinate systems) implement a common base interface called **`IEntity`**. This interface provides a uniform set of methods for selecting objects programmatically, checking selection status, and managing safe COM references.

---

## 📐 Role of IEntity

If you have a reference to an `IFace2` or `IEdge`, you can cast it directly to `IEntity` in C#:

```csharp
IFace2 face = (IFace2)selectedObj;
IEntity entity = (IEntity)face; // Casting to parent interface
```

Casting to `IEntity` enables direct selection operations without needing the `SelectByID2` coordinates or name lookup parameters.

---

## 💻 C# Code: Direct Selection via IEntity

The following code traverses all faces of a solid body, finds planar faces that have normal vectors pointing straight up along the positive Z axis, and selects them using `IEntity.Select4()`.

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class DirectSelector
{
    public void SelectTopPlanarFaces(IPartDoc part)
    {
        IModelDoc2 model = (IModelDoc2)part;
        object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);

        if (bodies == null) return;

        model.ClearSelection2(true);
        List<IEntity> facesToSelect = new List<IEntity>();

        foreach (object bodyObj in bodies)
        {
            IBody2 body = bodyObj as IBody2;
            if (body == null) continue;

            IFace2 face = body.GetFirstFace() as IFace2;
            while (face != null)
            {
                ISurface surface = face.GetSurface() as ISurface;
                
                if (surface != null && surface.IsPlane())
                {
                    // Evaluate normal vector of planar face
                    double[] uvBounds = (double[])face.GetUVBounds();
                    double uMid = (uvBounds[0] + uvBounds[1]) / 2.0;
                    double vMid = (uvBounds[2] + uvBounds[3]) / 2.0;
                    double[] evalData = (double[])surface.Evaluate(uMid, vMid, 1);

                    if (evalData != null && evalData.Length >= 6)
                    {
                        double nz = evalData[5]; // Normal vector component along Z axis
                        
                        // Check if face sense is normal or inverted
                        if (!face.FaceInSurfaceSense())
                        {
                            nz = -nz;
                        }

                        // If the normal is pointing up along Z (nz > 0.99)
                        if (nz > 0.99)
                        {
                            // Cast face to IEntity
                            IEntity faceEntity = (IEntity)face;
                            facesToSelect.Add(faceEntity);
                        }
                    }
                }
                face = face.GetNextFace() as IFace2;
            }
        }

        // Programmatically select all identified top faces
        Console.WriteLine($"Selecting {facesToSelect.Count} top planar faces...");
        
        foreach (IEntity entity in facesToSelect)
        {
            // Select4 Parameters:
            // Append = true (add selection to active list)
            // Data = null (optional selection data params)
            bool success = entity.Select4(true, null);
            if (!success)
            {
                Console.WriteLine("Failed to select face entity.");
            }
        }
    }
}
```

---

## 🔒 Safe Entities and Persistent IDs (GetSafeEntity)

When rebuilding models, SolidWorks destroys and regenerates topological faces and edges. A reference pointer to an `IFace2` or `IEdge` will become invalid ("stale") after a model rebuild, causing subsequent code execution to crash.

To keep a persistent reference that survives rebuilds or switching document windows, use the **`GetSafeEntity`** method.

```csharp
public IEntity GetPersistentReference(IFace2 face)
{
    IEntity entity = (IEntity)face;
    
    // Retrieves a safe entity reference that remains valid after model modifications
    IEntity safeEntity = entity.GetSafeEntity() as IEntity;
    
    return safeEntity;
}
```

---

## 🛠 Core Methods of IEntity

| Method | Return Type | Description |
| :--- | :--- | :--- |
| `Select4(Append, Data)` | `bool` | Selects the entity directly. Set `Append` to `true` to select multiple items. |
| `GetSafeEntity()` | `object` | Returns a safe COM reference wrapper for the entity that survives model rebuilds. |
| `GetType()` | `int` | Returns the type code of the entity (`swSelectType_e`). |
| `GetSelectByIDString()` | `string` | Returns the internal unique selection string identifier of the entity. |
| `DeSelect()` | `void` | De-selects this specific entity from the active selection. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Direct Selection vs Selection Marks:**
    `IEntity.Select4` accepts `ISelectData` as its second argument. If you need to select entities with a specific **Selection Mark** (e.g. for creating features like Lofts or Fillets that expect marks), instantiate a `SelectData` object and set its `Mark` property:
    ```csharp
    ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
    ISelectData selData = selMgr.CreateSelectData();
    selData.Mark = 2; // Assign mark value
    entity.Select4(true, selData); // Select using mark
    ```
2.  **Assembly Component Context:**
    If you extract an `IEntity` from a part document, you cannot select it directly using `Select4` inside an assembly document. You must create an assembly-mapped proxy entity using `IComponent2.GetCorresponding()` to translate the part entity reference into the assembly context.

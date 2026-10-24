# 5.9. CommandManager Annotation Tab Reference in SolidWorks API (C#)

This reference maps every visual command on the **Annotation Tab** of the SOLIDWORKS CommandManager to its corresponding C# API interface, methods, and configurations.

---

## 🗺 CommandManager Annotation Tab API Mapping

| CommandManager Button | SOLIDWORKS API Interface | Primary Method / Property | Description |
| :--- | :--- | :--- | :--- |
| **Smart Dimension** | `IModelDoc2` | `AddDimension2` / `AddHorizontalDimension2` | Inserts manual dimensions on selected entities. |
| **Model Items** | `IDrawingDoc` | `InsertModelAnnotations3` | Imports driving annotations from the 3D model. |
| **Note** | `IModelDoc2` | `CreateNewNote3` | Places a drawing note annotation. |
| **Balloon / Auto** | `IDrawingDoc` | `InsertBOMBalloon2` / `AutoBalloon5` | Inserts custom or automated BOM index balloons. |
| **Magnetic Line** | `IView` | `AddMagneticLine` | Creates layout alignment paths for balloon notes. |
| **Surface Finish** | `IModelDoc2` | `InsertSurfaceFinishSymbol3` | Places a surface finish/roughness symbol. |
| **Weld Symbol** | `IModelDoc2` | `InsertWeldSymbol` | Places a weld symbol annotation. |
| **Hole Callout** | `IModelDoc2` | `InsertHoleCallout2` | Places a hole description linked to a hole feature. |
| **Geometric Tol.** | `IModelDoc2` | `InsertGtol` | Places a Geometric Dimensioning & Tolerancing (GD&T) block. |
| **Datum Feature** | `IModelDoc2` | `InsertDatumFeatureSymbol3` | Places a datum identification symbol (e.g. Datum A). |
| **Datum Target** | `IModelDoc2` | `InsertDatumTargetSymbol2` | Places a datum target coordinate locator. |
| **Blocks** | `IModelDoc2` | `InsertBlock` | Inserts a custom block definition sketch component. |
| **Center Mark** | `IModelDoc2` | `InsertCenterMark2` | Places center tick marks on circles and arcs. |
| **Centerline** | `IModelDoc2` | `InsertCenterLine2` | Places center axis lines between cylindrical geometries. |
| **Area Hatch/Fill** | `IView` / `IFace2` | `InsertAreaHatch2` | Fills closed profile regions with a cross-hatch. |
| **Revision Symbol** | `IDrawingDoc` | `InsertRevisionSymbol` | Places a revision tag linked to the Revision Table. |
| **Revision Cloud** | `IModelDoc2` | `InsertRevisionCloud` | Draws a revision cloud annotation boundary. |
| **Tables** | `IDrawingDoc` / `IView` | `InsertTableAnnotation2` / `InsertWeldmentTable` | Inserts BOM, General, Hole, Revision, or Weld Tables. |

---

## 💻 C# Code Templates for Annotation Commands

### 1. Inserting a Hole Callout (InsertHoleCallout2)
Hole callouts extract hole wizard properties (counterbore, depth, thread pitches). Select the circular edge of the hole before invoking the method.

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public void CreateHoleCallout(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    drawing.ActivateView(viewName);

    // 1. Select the circular edge of the hole in the view
    model.ClearSelection2(true);
    bool edgeSelected = model.Extension.SelectByID2("", "EDGE", 0.12, 0.15, 0.0, false, 0, null, 0);

    if (edgeSelected)
    {
        // 2. Insert the Hole Callout (coordinates locate the text placement)
        // InsertHoleCallout2 Parameters: X, Y, Z coordinates
        IHoleCallout callout = model.InsertHoleCallout2(0.15, 0.18, 0.0) as IHoleCallout;

        if (callout != null)
        {
            System.Diagnostics.Debug.WriteLine("Hole Callout successfully inserted.");
        }
    }
}
```

### 2. Inserting Center Marks & Centerlines
Center marks are added to selected circles/arcs. Centerlines are placed between two selected cylindrical faces or edges.

```csharp
public void AddCenterMarkAndLine(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    drawing.ActivateView(viewName);

    // 1. Add Center Mark
    model.ClearSelection2(true);
    model.Extension.SelectByID2("", "EDGE", 0.08, 0.08, 0.0, false, 0, null, 0);
    
    // InsertCenterMark2 parameters: Style (1=Standard), Size, showLines
    ICenterMark centerMark = model.InsertCenterMark2(1, 0.002, true) as ICenterMark;

    // 2. Add Centerline
    model.ClearSelection2(true);
    // Select two parallel edges of a cylinder
    model.Extension.SelectByID2("", "EDGE", 0.15, 0.08, 0.0, true, 0, null, 0);
    model.Extension.SelectByID2("", "EDGE", 0.17, 0.08, 0.0, true, 0, null, 0);

    // Insert centerline based on selection
    ICenterLine centerline = model.InsertCenterLine2(1) as ICenterLine;
}
```

### 3. Inserting Area Hatch/Fill (InsertAreaHatch2)
Cross-hatching fills a closed flat surface or loop region. Select the face or closed loop boundary first.

```csharp
public void HatchSelectedFace(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    drawing.ActivateView(viewName);

    model.ClearSelection2(true);
    // Select a closed region face in drawing view context
    bool faceSelected = model.Extension.SelectByID2("", "FACE", 0.1, 0.1, 0.0, false, 0, null, 0);

    if (faceSelected)
    {
        ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
        IView view = selMgr.GetSelectedObject6(1, -1) as IView;

        // Apply hatch to the selected face inside the view context
        // InsertAreaHatch2 parameters: PatternName, Scale, Angle
        IAreaHatch areaHatch = view.InsertAreaHatch2("ANSI31 (Iron/Brick)", 1.0, 0.0) as IAreaHatch;

        if (areaHatch != null)
        {
            Console.WriteLine("Hatch pattern successfully applied.");
        }
    }
}
```

### 4. Inserting Hole and Revision Tables
Hole tables list coordinates and sizes of all holes in a view. Revision tables track drawing modifications.

```csharp
public void InsertHoleAndRevisionTables(IDrawingDoc drawing, string viewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    
    // 1. Insert Hole Table
    model.ClearSelection2(true);
    model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
    ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
    IView view = selMgr.GetSelectedObject6(1, -1) as IView;

    if (view != null)
    {
        string holeTableTemplate = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\english\standard-hole.sldholtbt";
        
        // InsertHoleTable2 parameters:
        // UseAnchor, X, Y, AnchorType, TagStyle, Template
        HoleTableAnnotation holeTable = view.InsertHoleTable2(
            false, 0.35, 0.25, 
            (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft, 
            "A", // Tag prefix
            holeTableTemplate
        );
    }

    // 2. Insert Revision Table (sheet level annotation)
    string revTableTemplate = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\english\standard-revision.sldrevtbt";
    
    RevisionTableAnnotation revTable = drawing.InsertRevisionTable2(
        false, 0.02, 0.02, // Bottom-left coordinate placement
        (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_BottomLeft, 
        revTableTemplate
    );
}
```

---

## ⚠️ Important Developer Rules

1.  **Hole Callout Selection Scope:**
    Hole callouts (`InsertHoleCallout2`) must target edges generated by the **Hole Wizard** or extrusion features. Attempting to insert a hole callout on a standard revolved cylinder edge will fail.
2.  **Hatch Boundary Integrity:**
    `InsertAreaHatch2` requires the selection boundary to represent a **fully closed flat surface region**. If the boundary loop has gaps or is not planar, the method returns `null`.
3.  **Template Directory Integrity:**
    Table templates (`.sldholtbt`, `.sldrevtbt`, `.sldgentbt`) must exist on disk. Check paths dynamically using target language folders (e.g. `lang\english` or `lang\russian`).
4.  **Selecting Entities in View Context:**
    When adding Centerlines or Center Marks, the target entities must be selected **relative to the active view coordinate space**. Ensure `drawing.ActivateView(viewName)` is called before selections occur.

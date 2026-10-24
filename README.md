# SolidWorks API Knowledge Base (C# & .NET 8 / 9 / 10)

Welcome to the local knowledge base for **SolidWorks API** development using modern **C#** and **.NET 8 (LTS)**, **.NET 9**, and **.NET 10**. This database is designed as a structured, production-grade reference guide for CAD developers, automation engineers, and software architects.

The knowledge base is divided into logical categories. Each article contains detailed descriptions of COM interfaces, compile-ready C# code templates, reference tables for methods/variables, and breakdowns of real-world pitfalls.

---

## 📂 Structure of the Knowledge Base

### 1. Getting Started & Architecture
*   [**1.1. Connection Guide (Modern .NET 8 / 9 / 10)**](1_getting_started/1_1_connection_guide.md)  
    *Official connection guide: attaching via Windows ROT using `oleaut32.dll` P/Invoke, cold start via ProgID, targeting .NET 8/9/10, SDK-style `.csproj` setup, UAC elevation mismatch detection, and preventing orphaned background processes.*
*   [**1.2. Basic Concepts and Types**](1_getting_started/1_2_basic_concepts.md)  
    *Overview of the interface hierarchy (`ISldWorks`, `IModelDoc2`, `IPartDoc`, `IAssemblyDoc`, `IDrawingDoc`), type casting rules, and document type codes.*
*   [**1.3. SolidWorks Process Management & Anti-Ghosting**](1_getting_started/1_3_solidworks_process_management.md)  
    *How to prevent background `sldworks.exe` zombie processes, detect invisible ghost instances, handle UAC elevation barriers, and safely release unmanaged COM resources.*

### 2. Document Operations
*   [**2.1. Document Management**](2_documents/2_1_document_management.md)  
    *Creating, opening, and saving documents silently, checking the dirty flag, and exporting sheets/parts to PDF, DXF, STEP, and IGES.*
*   [**2.2. Custom Properties**](2_documents/2_2_custom_properties.md)  
    *Reading and writing user-defined properties (`CustomPropertyManager`), managing configuration-specific properties, and handling parametric formulas.*

### 3. Modeling & Selection
*   [**3.1. Selection Manager**](3_modeling_and_geometry/3_1_selection_manager.md)  
    *Using `ISelectionMgr`, selecting entities by coordinates or name via `SelectByID2`, and utilizing selection marks (Selection Marks).*
*   [**3.2. Sketching**](3_modeling_and_geometry/3_2_sketching.md)  
    *Creating and editing 2D and 3D sketches (`SketchManager`), drawing lines, circles, adding geometric relations, and dimensions.*
*   [**3.3. Features**](3_modeling_and_geometry/3_3_features.md)  
    *Creating extrusion bosses/cuts (`FeatureManager`), fillets, chamfers, modifying existing feature parameters, and forcing model rebuilds.*

### 4. Assemblies
*   [**4.1. Assembly Operations**](4_assemblies/4_1_assembly_operations.md)  
    *Adding components, positioning them using transformations (`MathTransform`), and applying assembly mates.*
*   [**4.2. Components Traversal**](4_assemblies/4_2_components_traversal.md)  
    *Recursively traversing the assembly components hierarchy using the `IComponent2` interface.*
*   [**4.3. Display States & Visuals**](4_assemblies/4_3_display_states.md)  
    *Managing assembly display states, component visibility, transparency overrides, and applying visual appearances (colors) using `IDisplayStateSetting`.*

### 5. Drawings & Annotations
*   [**5.1. Drawing Basics**](5_drawings/5_1_drawing_basics.md)  
    *Managing sheets (`ISheet`), formats, sheet scales, and traversing drawing views (`IView`).*
*   [**5.2. Annotations and BOM**](5_drawings/5_2_annotations_and_bom.md)  
    *Creating Bill of Materials (BOM) tables, mapping columns to custom properties, automatic balloon placement (AutoBalloon5), and removing duplicates.*
*   [**5.3. View Placement & Types**](5_drawings/5_3_view_placement_types.md)  
    *Creating standard base, projected, auxiliary, section, detail, and multi-body/flat-pattern drawing views programmatically.*
*   [**5.4. Dimensioning & Tolerancing**](5_drawings/5_4_dimensioning_and_tolerancing.md)  
    *Inserting model annotations, adding manual dimensions, coordinate/ordinate dimension layouts, editing tolerances, and placing drawing notes.*
*   [**5.5. Sheet Metal & Weldment Drawings**](5_drawings/5_5_sheet_metal_and_weldment_drawings.md)  
    *Documenting sheet metal flat patterns (View Palette drag-and-drop, bend lines, bend tables) and weldment cut-list tables, weld symbols, and multi-body visibility filtering.*
*   [**5.6. Advanced Sheets & Templates**](5_drawings/5_6_advanced_sheets_and_templates.md)  
    *Dynamic sheet setup (`SetupSheet6`), template reloading, adding/deleting sheets, and programmatic view scaling algorithms.*
*   [**5.7. Advanced BOM & Balloon Customization**](5_drawings/5_7_advanced_bom_and_balloons.md)  
    *BOM table sorting (`IBomTableSortData`), custom cell sizes, splitting/merging tables, dimension auto-arrangement, magnetic lines, and custom balloon styles.*
*   [**5.8. CommandManager Drawing Tab Reference**](5_drawings/5_8_drawing_tab_commands.md)  
    *API mapping for all commands on the Drawing Tab: Broken-out Sections, Break Views, Crop Views, Alternate Positions, Standard 3 Views, Relative Views, and replacing referenced models.*
*   [**5.9. CommandManager Annotation Tab Reference**](5_drawings/5_9_annotation_tab_commands.md)  
    *API mapping for all commands on the Annotation Tab: Hole Callouts, Weld Symbols, Surface Finishes, GTOLs, Datum Features, Blocks, Center Marks, Centerlines, Hatching, and advanced Tables.*
*   [**5.10. Sketching in Drawings**](5_drawings/5_10_sketching_in_drawings.md)  
    *Using the Sketch Tab commands inside drawings: drawing primitives (lines, circles, splines), applying relations, trim/offset/mirror operations, and smart dimensioning sketch items.*

### 6. Weldments & Cut-Lists
*   [**6.1. Weldment Cut-Lists**](6_weldments/6_1_cut_lists.md)  
    *Iterating through `CutListFolder` features, detecting profile vs. plate types, and generating parametric descriptions (`BOMDescription`).*

### 7. Materials
*   [**7.1. Material Libraries**](7_materials/7_1_material_library_sldmat.md)  
    *Structure of `.sldmat` material library files (XML UTF-16), physical property mapping in SI units, and programmatically adding materials.*

### 8. Performance & Optimization
*   [**8.1. Performance & Stability**](8_performance_and_best_practices/8_1_optimization.md)  
    *Disabling screen updates, disabling rebuilds, and wrapping API calls in optimization scopes.*
*   [**8.2. Process Management & Stacking Prevention**](8_performance_and_best_practices/8_2_process_management.md)  
    *Preventing background `sldworks.exe` ghost processes, detecting UAC elevation mismatches, and releasing COM references cleanly on exit.*

### 9. Geometry, Topology, and Modeler (B-Rep)
*   [**9.1. Bodies and Topology**](9_geometry_and_topology/9_1_bodies_and_topology.md)  
    *Traversing the Boundary Representation (B-Rep) structure: extracting `IBody2` components, listing `IFace2` surfaces, checking `IEdge` arrays, and loops classification.*
*   [**9.2. Surfaces and Curves**](9_geometry_and_topology/9_2_surfaces_and_curves.md)  
    *Geometric evaluations on `ISurface` and `ICurve`: finding surface normals, face area calculations, projecting points onto curves, and calculating edge length.*
*   [**9.3. Entities and Selection**](9_geometry_and_topology/9_3_entities_and_selection.md)  
    *Working with `IEntity`, converting COM references to entities, safe COM pointers (`GetSafeEntity`), and choosing items via `Select4`.*
*   [**9.4. Modeler and Temporary Bodies**](9_geometry_and_topology/9_4_modeler_and_temporary_bodies.md)  
    *Creating temporary body geometries in memory using `IModeler` and performing Boolean operations (Union, Subtract, Intersect).*

### 10. AI & Hardware Acceleration (GPU & NPU)
*   [**10.1. Local ONNX, GPU & NPU Automation**](10_ai_and_hardware_acceleration/10_1_local_onnx_gpu_npu_automation.md)  
    *In-process lightweight machine learning (1M–50M parameters) using ONNX Runtime with DirectML, CUDA, and NPU acceleration: B-Rep GNNs for DFM routing, automated weld bevel selection (ISO 9692-1), 3D geometric duplicate search, smart drawing orientation, and YOLOv11-nano visual drawing norm-control.*

---

## 🛠 Modern .NET Quick Start Rules

1.  **TargetFramework & Architecture:**  
    Always target 64-bit Windows (`<TargetFramework>net8.0-windows</TargetFramework>`, `net9.0-windows`, or `net10.0-windows`). Set `<PlatformTarget>x64</PlatformTarget>` explicitly because SOLIDWORKS is exclusively a 64-bit application.
2.  **Interop Assembly Packages:**  
    In modern SDK projects, prefer using the official NuGet packages: `SolidWorks.Interop.sldworks` and `SolidWorks.Interop.swconst`. If referencing local DLLs from `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\`, ensure `<EmbedInteropTypes>false</EmbedInteropTypes>` is set to avoid `InvalidCastException`.
3.  **No `Marshal.GetActiveObject` in .NET 8 / 9 / 10:**  
    `Marshal.GetActiveObject` was removed from the modern .NET BCL. Use direct Win32 `oleaut32.dll` P/Invoke to attach to running instances via CLSID `{96749377-3391-11D2-9EE3-00C04F797396}`.
4.  **Threading & Trimming:**  
    Always mark application entry points with `[STAThread]`. When publishing single-file executables, ensure `<PublishTrimmed>false</PublishTrimmed>` so that COM Runtime Callable Wrapper (RCW) metadata is preserved.
5.  **Memory & Zombie Process Management:**  
    Release root COM interfaces with `Marshal.ReleaseComObject()` and invoke `GC.Collect()` followed by `GC.WaitForPendingFinalizers()`. This prevents orphaned `sldworks.exe` processes from locking memory.

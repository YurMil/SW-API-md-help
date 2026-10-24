# 5.5. Sheet Metal & Weldment Drawings in SolidWorks using C#

This section details how to automate drawing outputs for sheet metal (flat pattern views, bend lines, and bend tables) and weldment/multibody parts (cut list tables, filtering body visibility, and inserting weld symbols).

---

## 📐 Flat Pattern and Bend Tables (Sheet Metal)

For sheet metal parts (`.SLDPRT`), you must place a flat pattern view on the drawing sheet to show the blank geometry, bend lines, and bend tags.

### Inserting Flat Pattern Drawing Views Natively

In SOLIDWORKS, the most robust way to insert a flat pattern view programmatically is using the **View Palette** (the drag-and-drop mechanism). This mimics the user interface behavior exactly, which avoids warning dialogs about deleting dimensions, automatically checks the **Flat pattern** checkbox in the PropertyManager under *More views*, and ensures the derived flat-pattern configuration is created.

If the View Palette method fails or is unavailable, you should fall back to `CreateFlatPatternViewFromModelView3` by passing the **parent configuration name** (e.g. `"Default"` or `"02"`), which signals SOLIDWORKS to auto-generate the flat pattern configuration.

### C# Code: Creating Flat Pattern Views and Inserting Bend Tables

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class SheetMetalDrawingManager
{
    public void InsertFlatPatternViewAndTable(IDrawingDoc drawing, string partPath, string parentConfig = "Default")
    {
        IModelDoc2 model = (IModelDoc2)drawing;
        double viewX = 0.15;
        double viewY = 0.15;
        IView flatPatternView = null;

        // 1. Try to drop from the View Palette (Best Practice, mimics UI drag-and-drop)
        drawing.GenerateViewPaletteViews(partPath);
        string[] paletteViewNames = (string[])drawing.GetDrawingPaletteViewNames();
        string flatPatternPaletteName = null;

        if (paletteViewNames != null)
        {
            foreach (string name in paletteViewNames)
            {
                string lowerName = name.ToLower();
                // Match English ("flat pattern") or Russian ("развертка" / "развёртка")
                if (lowerName.Contains("flat pattern") || lowerName.Contains("развертка") || lowerName.Contains("развёртка"))
                {
                    flatPatternPaletteName = name;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(flatPatternPaletteName))
        {
            Console.WriteLine($"Dropping flat pattern view '{flatPatternPaletteName}' from View Palette...");
            flatPatternView = drawing.DropDrawingViewFromPalette2(flatPatternPaletteName, viewX, viewY, 0.0);
        }
        else
        {
            // Fallback: Create Flat Pattern View directly from model using parent configuration name.
            // DO NOT pass the flat-pattern configuration name directly if it does not exist yet!
            Console.WriteLine("Flat Pattern not found in palette. Falling back to ModelView...");
            flatPatternView = drawing.CreateFlatPatternViewFromModelView3(
                partPath, 
                parentConfig, // Parent configuration (e.g. "Default" or "02")
                viewX, 
                viewY, 
                0.0, 
                false, // Show bend lines? (false = show bend lines)
                false  // Flip view?
            );
        }

        if (flatPatternView == null)
        {
            Console.WriteLine("Failed to create Flat Pattern View.");
            return;
        }

        flatPatternView.SetName2("FlatPatternView");
        
        // 2. Explicitly lock the configuration of the drawing view to the derived flat pattern config
        string flatConfigName = parentConfig + "SM-FLAT-PATTERN"; // SolidWorks default format: [Parent]SM-FLAT-PATTERN (no hyphen)
        flatPatternView.ReferencedConfiguration = flatConfigName;
        
        model.ClearSelection2(true);
        IModelDocExtension modelDocExt = model.Extension;
        modelDocExt.SelectByID2("FlatPatternView", "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        drawing.ChangeRefConfigurationOfFlatPatternView(partPath, flatConfigName);

        // 3. Ensure the flat pattern feature in the part configuration is unsuppressed
        try
        {
            ModelDoc2 partDoc = (ModelDoc2)flatPatternView.ReferencedDocument;
            if (partDoc != null)
            {
                string savedConfig = partDoc.ConfigurationManager.ActiveConfiguration.Name;
                if (partDoc.ShowConfiguration2(flatConfigName))
                {
                    FeatureManager featMgr = partDoc.FeatureManager;
                    FlatPatternFolder flatFolder = (FlatPatternFolder)featMgr.GetFlatPatternFolder();
                    if (flatFolder != null)
                    {
                        object[] flatPatterns = (object[])flatFolder.GetFlatPatterns();
                        if (flatPatterns != null)
                        {
                            foreach (object obj in flatPatterns)
                            {
                                Feature feat = (Feature)obj;
                                feat.Select2(false, 0);
                                partDoc.EditUnsuppress2();
                            }
                        }
                    }
                    partDoc.ShowConfiguration2(savedConfig);

                    // Save the part document to save the unsuppressed state of the configuration on disk
                    int saveErrors = 0;
                    int saveWarnings = 0;
                    partDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unsuppress warning: {ex.Message}");
        }

        // 4. Insert a Bend Table linked to the Flat Pattern View
        string bendTableTemplate = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\english\bendtable-standard.sldbndtbt";
        double tableX = 0.25;
        double tableY = 0.2;

        BendTableAnnotation bendTable = flatPatternView.InsertBendTable(
            false, // Use anchor point?
            tableX, 
            tableY, 
            (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopRight, 
            "A", // Starting datum tag
            bendTableTemplate
        );

        if (bendTable != null)
        {
            Console.WriteLine("Bend Table successfully inserted.");
        }

        model.ForceRebuild3(true);
    }
}
```

---

## 🛠 Weldment Cut List Tables (Weldments)

Weldment drawings do not use a standard BOM table. Instead, they require a **Weldment Cut List Table** which groups components by cut-list folder properties.

```csharp
public void InsertCutListTable(IDrawingDoc drawing, string weldmentViewName)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    
    // 1. Select the drawing view representing the weldment assembly
    model.ClearSelection2(true);
    bool selectSuccess = model.Extension.SelectByID2(weldmentViewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);

    if (selectSuccess)
    {
        ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
        IView weldView = selMgr.GetSelectedObject6(1, -1) as IView;

        if (weldView != null)
        {
            // 2. Insert Weldment Cut List Table
            string cutListTemplate = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\english\cut list.sldwldtbt";
            double tableX = 0.28;
            double tableY = 0.2;

            WeldmentCutListAnnotation cutListTable = weldView.InsertWeldmentTable(
                false, // Use anchor point
                tableX, 
                tableY, 
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft, 
                "Default", // Configuration name
                cutListTemplate
            );

            if (cutListTable != null)
            {
                Console.WriteLine("Weldment Cut List Table inserted successfully.");
            }
        }
    }
}
```

---

## 👁 Filtering Body Visibility in Multibody Drawing Views

In multibody or weldment parts, you often need to show only a single body (e.g. a specific gusset plate) inside a drawing view. This is achieved by passing an array of visible bodies to the `IView.ISetBodies` method.

```csharp
public void FilterDrawingViewToSingleBody(IDrawingDoc drawing, string viewName, IBody2 bodyToShow)
{
    IModelDoc2 model = (IModelDoc2)drawing;
    
    model.ClearSelection2(true);
    model.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
    
    ISelectionMgr selMgr = model.SelectionManager as ISelectionMgr;
    IView view = selMgr.GetSelectedObject6(1, -1) as IView;

    if (view != null)
    {
        // 1. Define the array of bodies to remain visible in this view
        IBody2[] visibleBodies = new IBody2[] { bodyToShow };
        
        // 2. Apply body visibility override
        // ISetBodies(int count, ref IBody2 bodiesArray)
        // Note: in C# interops, pass the array using the ref keyword
        view.ISetBodies(visibleBodies.Length, ref visibleBodies[0]);
        
        Console.WriteLine($"Drawing View '{viewName}' filtered to show {visibleBodies.Length} body.");
        
        model.ForceRebuild3(false);
    }
}
```

---

## 🛠 Sheet Metal and Weldment Drawing API Reference

### Anchor Type Enumeration (`swBOMConfigurationAnchorType_e`):
*   `swBOMConfigurationAnchor_TopLeft` (1) — Anchors table from top-left corner.
*   `swBOMConfigurationAnchor_TopRight` (2) — Anchors table from top-right corner.
*   `swBOMConfigurationAnchor_BottomLeft` (3) — Anchors table from bottom-left corner.
*   `swBOMConfigurationAnchor_BottomRight` (4) — Anchors table from bottom-right corner.

### Key API Methods:

| Method | Class / Interface | Description |
| :--- | :--- | :--- |
| `CreateFlatPatternViewFromModelView2(...)` | `IDrawingDoc` | Inserts a flat pattern view onto the sheet. |
| `InsertBendTable(...)` | `IView` | Places a bend table annotation linked to a view. |
| `InsertWeldmentTable(...)` | `IView` | Places a weldment cut-list table annotation linked to a view. |
| `ISetBodies(count, ref body)` | `IView` | Sets the array of bodies visible in a multibody part view. |
| `GetBodiesCount()` | `IView` | Returns the total count of bodies inside the view's model. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Missing Bend Table Template Paths:**
    Default template installation paths vary across system configurations depending on local language installations (e.g. `\lang\english\` or `\lang\russian\`). Always check if the template exists on disk before calling `InsertBendTable()`, or prompt the user for the local path.
2.  **Flat Pattern Views Fail on Assembly Models:**
    `CreateFlatPatternViewFromModelView2` only accepts part files (`.SLDPRT`) as the source document path. If your source document is an assembly, traverse the components first to get the individual sheet metal parts.
3.  **ISetBodies Reference Errors:**
    When calling `ISetBodies()`, ensure the `IBody2` objects belong strictly to the part model referenced in the drawing view. Passing a body from a different part file will trigger a silent failure. Use `IView.GetCorresponding()` to map body references if needed.

---

## 📐 Умная простановка размеров деталей из листового металла (Smart Dimensioning)

Стандартные методы `AutoDimension` и `InsertModelAnnotations4` часто приводят к загромождению чертежа сотнями ненужных размеров для деталей из листового металла (включая все углы гибов, размеры эскизов, толщину на каждом вырезе и т.д.).

Чтобы чертеж был чистым и соответствовал правилам оформления листового металла, рекомендуется использовать **программную классификацию кромок (Edge Classification)** и выборочную простановку размеров (`AddDimension2`).

### Ключевые принципы оформления деталей из листового металла:
1. **Габаритные размеры**: Ширина и высота по крайним точкам (на всех видах).
2. **Толщина металла**: Только 1 размер для демонстрации толщины листа (не на развертке).
3. **Отверстия**: Группировка по диаметру. Указание диаметра с префиксом количества (например, `5x ⌀8.5mm`).
4. **Позиции отверстий**: Только расстояния от базовых кромок до первого отверстия в группе (привязка).
5. **Вырезы (не круглые)**: Ширина, высота и 2 размера привязки от краев детали.
6. **Гибы (Bends)**: Длина отгиба (Flange length) и радиус гиба. Размеры углов обычно не ставятся.
7. **Развертка (Flat Pattern)**: Только габариты, отверстия и размеры от краев до **линий гиба** (Bend Lines).

### Архитектура модуля (C#)
Для реализации такого подхода необходимо читать геометрию непосредственно из вида (`IView.GetVisibleEntities2`), преобразовывать координаты модели в координаты листа чертежа (`IView.ModelToViewTransform`) и определять фичерсы, которым принадлежат кромки (`IFace2.GetFeature`).

#### 1. Чтение геометрии вида и преобразование координат
Размеры в SOLIDWORKS (`AddDimension2`) ставятся в координатах листа (метрах от нижнего левого угла основной надписи), а геометрия кромок (`IEdge`, `IVertex`) хранится в координатах модели.

```csharp
// Получение матрицы трансформации Вид -> Лист
IMathTransform xform = (IMathTransform)view.ModelToViewTransform;
IMathUtility mathUtil = (IMathUtility)swApp.GetMathUtility();

// Конвертация точки
IMathPoint mp = (IMathPoint)mathUtil.CreatePoint(modelPointArray);
IMathPoint tp = (IMathPoint)mp.MultiplyTransform(xform);
double[] sheetCoords = (double[])tp.ArrayData;
```

#### 2. Определение типа элемента (Гиб, Вырез)
Кромка сама по себе не знает, чем она является. Нужно получить смежные грани, а от них — фичерс.

```csharp
Feature GetEdgeFeature(Edge edge)
{
    object[] faces = (object[])edge.GetTwoAdjacentFaces2();
    if (faces != null && faces.Length > 0)
    {
        Face2 face = (Face2)faces[0];
        return (Feature)face.GetFeature();
    }
    return null;
}

// Примеры типов фичерсов (Feature.GetTypeName2):
// "EdgeFlange", "Hem", "Jog", "SketchBend", "SM3dBend", "SMMiteredFlange", "MiterFlange"
// "NormalCut", "CutExtrude"
```

#### 3. Размеры линий гиба на развертке (Flat Pattern)
На развертке линии гиба не являются кромками (`IEdge`), это сегменты эскиза (`ISketchSegment`), и их нужно получать через специальный метод `IView.GetBendLines()`.

```csharp
if (view.IsFlatPatternView())
{
    object[] bendLines = (object[])view.GetBendLines();
    if (bendLines != null)
    {
        foreach (SketchSegment bendSeg in bendLines)
        {
            // Для выбора сегмента используем метод Select4 самого сегмента:
            // bendSeg.Select4(append, selData);
            // Запрещено пытаться привести SketchSegment к (Entity) для выбора!
        }
    }
}
```

#### 4. Подавление всплывающих диалоговых окон
При программном вызове `AddDimension2` SOLIDWORKS попытается открыть окно "Ввод размера", что заморозит выполнение скрипта (или создаст "призрачный" процесс).

Обязательно подавляйте эту опцию:
```csharp
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);
// ... создание размеров ...
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, true);
```

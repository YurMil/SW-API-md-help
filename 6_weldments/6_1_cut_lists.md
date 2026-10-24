# 6.1. Weldment Cut-Lists in SolidWorks

Weldments in SolidWorks allow designing frames, profiles, and structural plates within a single part file (`.SLDPRT`). Instead of creating individual part files for every element, SolidWorks groups matching solid bodies into virtual folders called cut-lists (`CutListFolder`).

In drawings, these groupings are compiled into a **Weldment Cut List** table.

---

## 🔍 Identifying Weldment Cut-List Folders

Each cut-list folder is represented in the FeatureManager design tree as an `IFeature` object that can be cast to an `IBodyFolder` interface.

### Criteria for filtering valid cut-list folders:
*   The feature type name (`IFeature.GetTypeName2()`) must be `"CutListFolder"`.
*   The count of bodies within the folder (`IBodyFolder.GetBodyCount()`) must be greater than `0` (ignore empty/ghost folders).

### Cut-List Folder Types (`IBodyFolder.GetCutListType()`):
*   **`3`** — Structural Member Profile (beams, tubes, channels).
*   **`2`** — Sheet Metal or Flat Plates.

---

## 💻 C# Code: Iterating and Renumbering Cut-Lists

Here is a C# script to traverse part features, extract cut-lists, identify their category, and write custom properties and parametric description formulas.

```csharp
using System;
using System.Diagnostics;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class WeldmentManager
{
    public void ProcessWeldmentCutList(IPartDoc part, string baseId)
    {
        IModelDoc2 model = (IModelDoc2)part;
        IFeature feature = part.FirstFeature() as IFeature;
        
        int cutListIndex = 1; // Counter for index numbering

        while (feature != null)
        {
            string featType = feature.GetTypeName2();

            // 1. Verify if the feature is a cut-list folder
            if (featType == "CutListFolder")
            {
                IBodyFolder bodyFolder = feature.GetSpecificFeature2() as IBodyFolder;
                
                // Inspect body count (exclude empty folders)
                if (bodyFolder != null && bodyFolder.GetBodyCount() > 0)
                {
                    int cutType = bodyFolder.GetCutListType();
                    ICustomPropertyManager propMgr = feature.CustomPropertyManager;

                    // Generate a unique item index number (e.g. "EST-P100.1", "EST-P100.2")
                    string itemNumber = $"{baseId}.{cutListIndex}";
                    
                    // Write BOM identification properties
                    WriteOrUpdateProperty(propMgr, "IDNumber", itemNumber);
                    WriteOrUpdateProperty(propMgr, "BOMIDNumber", itemNumber);

                    // 2. Generate descriptions based on profile type
                    string featureName = feature.Name; // Feature name in tree (e.g. "HEA 100<1>")
                    string fileName = System.IO.Path.GetFileName(model.GetPathName());

                    if (cutType == 3) // Structural profile member (e.g. HEA, channel, angle)
                    {
                        // Retrieve the raw system "Description" added by the structural member profile
                        string rawDesc = GetPropertyRaw(propMgr, "Description");
                        
                        // Strip out previous length suffix tags
                        string cleanDesc = StripLengthSuffix(rawDesc);

                        // Generate a parametric description formula containing length
                        // SolidWorks formula syntax: "ProfileName, L=" + "LENGTH@@@FeatureName@FileName"
                        string parametricExpr = $"{cleanDesc}, L=\"LENGTH@@@{featureName}@{fileName}\"";
                        
                        WriteOrUpdateProperty(propMgr, "Description", parametricExpr);
                        WriteOrUpdateProperty(propMgr, "BOMDescription", parametricExpr);
                    }
                    else if (cutType == 2) // Sheet metal / flat plate
                    {
                        // Generate a parametric plate description using width, thickness, and length properties
                        // Note: Always use system tokens: "SW-Sheet Metal Thickness", "SW-Bounding Box Width", "SW-Bounding Box Length"
                        string thicknessExpr = $"\"SW-Sheet Metal Thickness@@@{featureName}@{fileName}\"";
                        string widthExpr = $"\"SW-Bounding Box Width@@@{featureName}@{fileName}\"";
                        string lengthExpr = $"\"SW-Bounding Box Length@@@{featureName}@{fileName}\"";

                        string plateExpr = $"PL{thicknessExpr}x{widthExpr}x{lengthExpr}";

                        // Write parametric plate code to BOMDescription
                        WriteOrUpdateProperty(propMgr, "BOMDescription", plateExpr);
                        WriteOrUpdateProperty(propMgr, "Description", "Plate"); // Static tree name
                    }

                    cutListIndex++;
                }
            }
            
            // Advance to next feature
            feature = feature.GetNextFeature() as IFeature;
        }

        // Write properties to the model root
        ICustomPropertyManager partPropMgr = model.Extension.CustomPropertyManager[""];
        WriteOrUpdateProperty(partPropMgr, "IDNumber", baseId);
        WriteOrUpdateProperty(partPropMgr, "BOMIDNumber", baseId);
        WriteOrUpdateProperty(partPropMgr, "BOMDescription", "WELDMENT");

        // Force rebuild to commit parametric values
        model.ForceRebuild3(false);
    }

    private void WriteOrUpdateProperty(ICustomPropertyManager propMgr, string name, string value)
    {
        // 30 = swCustomInfoText, 2 = swCustomPropertyReplaceValue
        int res = propMgr.Add3(name, 30, value, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
        if (res == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_Failed)
        {
            propMgr.Set2(name, value);
        }
    }

    private string GetPropertyRaw(ICustomPropertyManager propMgr, string name)
    {
        string raw = "";
        string resolved = "";
        bool wasResolved = false;
        bool link = false;
        propMgr.Get6(name, false, out raw, out resolved, out wasResolved, out link);
        return string.IsNullOrEmpty(raw) ? resolved : raw;
    }

    private string StripLengthSuffix(string description)
    {
        if (string.IsNullOrEmpty(description)) return "Profile";
        int idx = description.IndexOf(", L=");
        if (idx > 0)
        {
            return description.Substring(0, idx);
        }
        return description;
    }
}
```

---

## 🛠 Drawing Column Mapping to Cut-List Properties

To display custom properties in your drawing tables, map the table columns of `IBomTableAnnotation` to the properties written to the cut-list folders:

```csharp
bomTable.SetColumnCustomProperty(colIndexID, "BOMIDNumber");
bomTable.SetColumnCustomProperty(colIndexDesc, "BOMDescription");
bomTable.SetColumnCustomProperty(colIndexMat, "Material");
```

---

## ⚠️ Known Issues and Developer Rules

### 1. Parametric Formulas Display as Text Strings
*   **Symptom:** The drawing table cell shows the raw text formula `PL"SW-Sheet Metal Thickness...` instead of evaluating to `PL10x150x200`.
*   **Cause:** The token strings do not include the required system prefix `SW-`. SolidWorks ignores compound formulas that lack correct system properties.
*   **Solution:** Verify the formula utilizes `SW-Sheet Metal Thickness`, `SW-Bounding Box Width`, and `SW-Bounding Box Length` tokens enclosed in double quotes.

### 2. Table Cells Do Not Update After Rebuilding
*   **Symptom:** Cut-list properties updated inside the 3D model, but drawing tables continue to show stale values.
*   **Solution:** Due to COM caching in SolidWorks, if drawing rebuilds fail to refresh table annotations, apply a direct cell text override as a last resort:
    ```csharp
    ITableAnnotation table = bomTable as ITableAnnotation;
    table.Text2[row, column, false] = "EST-P100.1";
    ```
    Ensure that the underlying model property itself is correct before applying this override.

# 2.2. Custom Properties in SolidWorks using C#

Custom properties are metadata stored in SolidWorks parts, assemblies, and drawings. This metadata is automatically read by drawing title blocks, Bill of Materials (BOM) tables, and external ERP/PDM systems.

Access to custom properties is managed via the `ICustomPropertyManager` interface.

---

## 📂 Global Properties vs Configuration-Specific Properties

In SolidWorks 3D documents (parts and assemblies), properties are split into:
1.  **Global Properties (Custom):** Apply to the entire document.
2.  **Configuration-Specific Properties:** Apply only to a designated configuration of the model (e.g. varying length parameters for different configurations).

Drawing documents (`.SLDDRW`) only support global properties.

### Obtaining the Property Manager in C#:

```csharp
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// 1. Get GLOBAL properties of the document
ICustomPropertyManager globalPropMgr = model.Extension.CustomPropertyManager[""];

// 2. Get properties for a specific CONFIGURATION
string configName = "Default"; // Configuration name
ICustomPropertyManager configPropMgr = model.Extension.CustomPropertyManager[configName];
```

---

## 📝 Writing and Updating Properties

To create or modify properties, use `Add3` followed by `Set2` as a fallback updater.

*   `Add3(Name, Type, Value, OverwriteOption)` — Creates a new property.
    *   `Type`: Text parameters are defined using value `30` (equivalent to `swCustomInfoType_e.swCustomInfoText`).
    *   `OverwriteOption`: Value `2` (`swCustomPropertyAddOption_e.swCustomPropertyReplaceValue`) overwrites the value if the property already exists.
*   `Set2(Name, Value)` — Updates an existing property.

```csharp
public void WriteProperty(IModelDoc2 model, string configName, string propName, string propValue)
{
    // Retrieve the custom property manager
    ICustomPropertyManager propMgr = model.Extension.CustomPropertyManager[configName];

    // Add or replace the property
    // Type 30 = swCustomInfoText
    // Option 2 = swCustomPropertyReplaceValue (overwrite if existing)
    int result = propMgr.Add3(propName, 30, propValue, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

    if (result == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
    {
        System.Diagnostics.Debug.WriteLine($"Property '{propName}' successfully written.");
    }
    else
    {
        // Fallback to Set2 if Add3 did not apply changes
        propMgr.Set2(propName, propValue);
    }
}
```

---

## 🔍 Reading Properties (Raw vs Resolved Values)

SolidWorks supports parametric properties (e.g., links like `$"SW-Mass@@Default@Part1.SLDPRT"`). When reading these properties, distinguish between the raw formula and the resolved evaluated value.

Use the `Get6` method:
*   `rawVal` — Returns the raw formula string (e.g., `"\"SW-Mass@@Default@Part1.SLDPRT\""`).
*   `resolvedVal` — Returns the evaluated string value (e.g., `"7.85 kg"`).

```csharp
public string ReadResolvedProperty(IModelDoc2 model, string configName, string propName)
{
    ICustomPropertyManager propMgr = model.Extension.CustomPropertyManager[configName];

    string rawVal = "";
    string resolvedVal = "";
    bool wasResolved = false;
    bool linkToProperty = false;

    // Read property using the Get6 method
    int result = propMgr.Get6(propName, false, out rawVal, out resolvedVal, out wasResolved, out linkToProperty);

    if (result == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_ResolvedValue || wasResolved)
    {
        return resolvedVal;
    }

    return string.Empty; // Property not found or could not be resolved
}
```

---

## ❌ Deleting Properties

Use the `Delete2` method to delete custom properties:

```csharp
public void RemoveProperty(IModelDoc2 model, string configName, string propName)
{
    ICustomPropertyManager propMgr = model.Extension.CustomPropertyManager[configName];
    int result = propMgr.Delete2(propName);
    
    if (result == (int)swCustomInfoDeleteResult_e.swCustomInfoDeleteResult_Success)
    {
        System.Diagnostics.Debug.WriteLine($"Property '{propName}' successfully deleted.");
    }
}
```

---

## 🛠 Methods and Return Codes of ICustomPropertyManager

### Get6 Return Codes (`swCustomInfoGetResult_e`):
*   `0` — `swCustomInfoGetResult_NotExists` (Property does not exist).
*   `1` — `swCustomInfoGetResult_ResolvedValue` (Successfully read resolved value).

### Add3 Return Codes (`swCustomInfoAddResult_e`):
*   `0` — `swCustomInfoAddResult_Failed` (Failed to add).
*   `1` — `swCustomInfoAddResult_SuccessfullyAdded` (Successfully added).
*   `2` — `swCustomInfoAddResult_AddedOrChanged` (Added or overwritten).

---

## ⚠️ Known Issues and Developer Guidance

1.  **File Renaming and Formatted Formulas:**
    If a formula (e.g., `BOMDescription`) contains reference paths to the file name (like `@Part1.SLDPRT`), renaming the file on disk will break the formula unless you update the referenced file name within the formula string.
2.  **Updating drawing tables:**
    After modifying custom properties programmatically, rebuild the model to ensure all linked tables and drawings refresh their displays:
    ```csharp
    model.ForceRebuild3(false);
    ```
3.  **Active Configurations:**
    Pass an empty string `""` to the custom property manager indexer to access global document properties. Otherwise, provide the exact name of a valid configuration.

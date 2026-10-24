# 7.1. Editing Material Libraries (.sldmat) in SolidWorks

Material libraries in SolidWorks are saved in XML-formatted database files with the `.sldmat` extension. These files are encoded using **UTF-16 LE** (Unicode).

This guide describes the structure of `.sldmat` XML files, physical property mapping in SI units, and C# code to safely append materials without creating duplicates.

---

## 📂 Structure of .sldmat XML Files

The root XML node includes the SolidWorks materials schema namespaces. Materials are grouped into categories using `<classification>` nodes:

```xml
<?xml version="1.0" encoding="UTF-16" standalone="no"?>
<mstns:materials xmlns:mstns="http://www.solidworks.com/sldmaterials" xmlns:sldcolorswatch="http://www.solidworks.com/sldcolorswatch" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.solidworks.com/sldmaterials http://www.solidworks.com/sldmaterials/sldmaterials.xsd">
  
  <classification name="Carbon Steel OTHER">
    
    <material name="P250GH" description="1.0460 / P250GH pressure vessel steel" propertysource="" appdata="">
      <xhatch name="ISO (Steel)" angle="0" scale="1"/>
      <shaders>
        <pwshader2 name="cast carbon steel" path="\metal\steel\cast carbon steel.p2m"/>
      </shaders>
      <swatchcolor RGB="EB7749">
        <sldcolorswatch:Optical Ambient="1.000000" Transparency="0.000000" Diffuse="1.000000" Specularity="1.000000" Shininess="0.000000" Emission="0.000000"/>
      </swatchcolor>
      
      <physicalproperties>
        <EX displayname="Elastic Modulus" value="2E+011" usepropertycurve="0"/>
        <NUXY displayname="Poisson's Ratio" value="0.30" usepropertycurve="0"/>
        <GXY displayname="Shear Modulus" value="7.69E+010" usepropertycurve="0"/>
        <ALPX displayname="Thermal Expansion Coefficient" value="1.2e-005" usepropertycurve="0"/>
        <DENS displayname="Mass Density" value="7850" usepropertycurve="0"/>
        <KX displayname="Thermal Conductivity" value="30" usepropertycurve="0"/>
        <C displayname="Specific Heat" value="500" usepropertycurve="0"/>
        <SIGXT displayname="Tensile Strength" value="410000000" usepropertycurve="0"/>
        <SIGYLD displayname="Yield Strength" value="250000000" usepropertycurve="0"/>
      </physicalproperties>
      
      <custom/>
    </material>

  </classification>
  
</mstns:materials>
```

---

## 📐 Physical Property Mappings and SI Units

SolidWorks stores physical properties within `.sldmat` libraries strictly using **SI metric units**. When updating materials programmatically, convert properties using these units:

| XML Tag | Property Description | SI Unit in XML | Carbon Steel Example |
| :--- | :--- | :--- | :--- |
| **`EX`** | Elastic Modulus (Young's) | Pascals (Pa) | `2E+011` (200 GPa) |
| **`NUXY`** | Poisson's Ratio | Dimensionless | `0.30` |
| **`GXY`** | Shear Modulus | Pascals (Pa) | `7.69E+010` (76.9 GPa) |
| **`ALPX`** | Thermal Expansion Coefficient | 1 / Kelvin (1/K) | `1.2e-005` |
| **`DENS`** | Mass Density | kg / cubic meter (kg/m³) | `7850` |
| **`KX`** | Thermal Conductivity | W / (meter * Kelvin) | `30` |
| **`C`** | Specific Heat | J / (kg * Kelvin) | `500` |
| **`SIGXT`**| Tensile Strength | Pascals (Pa) | `410000000` (410 MPa) |
| **`SIGYLD`**| Yield Strength | Pascals (Pa) | `250000000` (250 MPa) |

---

## 💻 Modifying .sldmat Files Programmatically

Since `.sldmat` database files are encoded in UTF-16, ensure the target encoding and XML namespaces are handled properly during edits to avoid corrupting the file structure.

Below is a C# example to add a new material node to an existing `.sldmat` database.

```csharp
using System;
using System.IO;
using System.Text;
using System.Xml;

public class MaterialLibraryEditor
{
    public void AddOrUpdateMaterial(string libraryPath, string targetCategory, string materialName, string description)
    {
        // 1. Create a backup file
        string backupPath = libraryPath + ".bak";
        File.Copy(libraryPath, backupPath, true);

        // 2. Initialize XmlDocument
        XmlDocument doc = new XmlDocument();
        doc.PreserveWhitespace = true;
        
        // Load file (automatically parses UTF-16)
        doc.Load(libraryPath);

        // 3. Define XML Namespaces
        XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("ns", "http://www.solidworks.com/sldmaterials");

        string xpath = $"//ns:classification[@name='{targetCategory}']";
        XmlNode categoryNode = doc.SelectSingleNode(xpath, nsMgr);

        if (categoryNode == null)
        {
            throw new Exception($"Classification category '{targetCategory}' not found in the library.");
        }

        // 4. Verify if material already exists to prevent duplicates
        XmlNode existingMaterial = categoryNode.SelectSingleNode($"ns:material[@name='{materialName}']", nsMgr);

        if (existingMaterial != null)
        {
            // Update description if material exists
            existingMaterial.Attributes["description"].Value = description;
            Console.WriteLine($"Material '{materialName}' already exists. Description updated.");
        }
        else
        {
            // Create a new material element with properties
            XmlElement newMat = doc.CreateElement("material", "http://www.solidworks.com/sldmaterials");
            newMat.SetAttribute("name", materialName);
            newMat.SetAttribute("description", description);
            newMat.SetAttribute("propertysource", "");
            newMat.SetAttribute("appdata", "");

            // Define structural inner XML
            newMat.InnerXml = @"
                <xhatch name=""ISO (Steel)"" angle=""0"" scale=""1""/>
                <physicalproperties>
                    <EX displayname=""Elastic Modulus"" value=""2E+011"" usepropertycurve=""0""/>
                    <NUXY displayname=""Poisson's Ratio"" value=""0.30"" usepropertycurve=""0""/>
                    <DENS displayname=""Mass Density"" value=""7850"" usepropertycurve=""0""/>
                    <SIGXT displayname=""Tensile Strength"" value=""400000000"" usepropertycurve=""0""/>
                    <SIGYLD displayname=""Yield Strength"" value=""235000000"" usepropertycurve=""0""/>
                </physicalproperties>
                <custom/>";

            categoryNode.AppendChild(newMat);
            Console.WriteLine($"Material '{materialName}' successfully added to classification '{targetCategory}'.");
        }

        // 5. Save using UTF-16 encoding with preserved indentation formatting
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = Encoding.Unicode, // UTF-16 LE
            Indent = false, // Preserve original layout structure
            IndentChars = ""
        };

        using (XmlWriter writer = XmlWriter.Create(libraryPath, settings))
        {
            doc.Save(writer);
        }
    }
}
```

---

## 🛠 Verifying Material Library Changes

After editing `.sldmat` XML libraries, perform these verification checks:
1.  **XML Syntax Check:**
    Open the edited `.sldmat` file inside a web browser (e.g. Chrome or Edge). If the XML structure is malformed, the browser will report a parsing error.
2.  **Encoding Verification:**
    Open the database inside an editor (like Notepad++) and verify that the encoding is set to `UTF-16 LE` (or `UCS-2 LE BOM`). SolidWorks will fail to load the library if the encoding is wrong.
3.  **Restart SolidWorks:**
    SolidWorks caches material definitions at startup. Restart the SolidWorks application to force it to reload the modified `.sldmat` file definitions.
4.  **PowerShell Verification Check:**
    Check classification nodes programmatically using PowerShell:
    ```powershell
    [xml]$xml = Get-Content -Path "C:\path\to\library.sldmat" -Encoding Unicode
    $xml.materials.classification | 
        Where-Object { $_.name -eq "Carbon Steel OTHER" } | 
        Select-Object -ExpandProperty material | 
        Select-Object name, description
    ```

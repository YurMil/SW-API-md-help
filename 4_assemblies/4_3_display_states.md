# 4.3. Display States and Visual Customization in SolidWorks using C#

**Display States** in SolidWorks allow developers to control visual properties of components (such as visibility, transparency, display modes, and appearances/colors) inside assemblies without creating new physical configurations. This is extremely useful for generating customized drawing views, highlighted assembly states, and rendering setups.

---

## 📐 Display States vs Configurations

*   **Configurations**: Define variations of model geometry (e.g. suppressed features, part sizes, different part files). Editing configurations changes the physical mass properties and geometry tree of the assembly.
*   **Display States**: Define variations of visual styling only (e.g. hiding bolt components, changing plate color, making a cover transparent). Changing display states is fast and does not trigger model re-evaluation or rebuild geometry cycles.

---

## 💻 C# Code: Activating and Querying Display States

You can query the available display states of the active configuration and switch between them using the `IConfiguration` interface.

```csharp
using System;
using SolidWorks.Interop.sldworks;

public class DisplayStateManager
{
    public void ManageActiveDisplayStates(IModelDoc2 model)
    {
        // 1. Get the active configuration
        IConfiguration config = model.GetActiveConfiguration() as IConfiguration;
        if (config == null) return;

        // 2. Query available display states
        string[] displayStates = (string[])config.GetDisplayStates();
        Console.WriteLine($"Total Display States found: {displayStates.Length}");

        foreach (string dsName in displayStates)
        {
            Console.WriteLine($" - Display State: {dsName}");
        }

        // 3. Get the active display state name
        string activeState = config.ActiveDisplayState;
        Console.WriteLine($"Currently Active Display State: {activeState}");

        // 4. Switch to another display state
        if (displayStates.Length > 1)
        {
            // Switch active display state
            config.ActiveDisplayState = displayStates[1]; 
            model.GraphicsRedraw2(); // Repaint display
        }
    }
}
```

---

## 🔍 Fine-Tuning with IDisplayStateSetting

To query or apply visibility, transparency, or display modes to specific components in specific display states, use the **`IDisplayStateSetting`** interface. 

### Setting Scopes (`swDisplayStateOpts_e`):
*   `swThisDisplayState` (0) — Applies settings only to the active display state.
*   `swAllDisplayState` (1) — Applies settings to all display states.
*   `swSpecifyDisplayState` (2) — Applies settings to specified display states by name.

### C# Code: Managing Component Visibility, Transparency, and Display Modes

The following class demonstrates configuring a `DisplayStateSetting` object to hide and make specific components transparent in a designated display state.

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public class DisplayStateConfigurator
{
    public void ApplyVisualOverrides(IModelDoc2 model, IComponent2 component)
    {
        IModelDocExtension ext = model.Extension;
        
        // 1. Instantiate the display state setting object
        // We target custom specified display states
        DisplayStateSetting dsSetting = (DisplayStateSetting)ext.GetDisplayStateSetting((int)swDisplayStateOpts_e.swSpecifyDisplayState);
        
        // Set target option scope
        dsSetting.Option = (int)swDisplayStateOpts_e.swSpecifyDisplayState;
        
        // 2. Define the target Display States by name
        string[] targetStates = new string[] { "Default_Display State", "HighlightState" };
        dsSetting.Names = targetStates;

        // 3. Define the target components/entities
        object[] targetEntities = new object[] { component };
        dsSetting.Entities = targetEntities;

        // 4. Override Visibility: Hide component
        // Values: swComponentVisibilityState_e.swComponentHidden (0), swComponentVisible (1)
        int[] visibilityData = new int[] { (int)swComponentVisibilityState_e.swComponentHidden };
        ext.Visibility = dsSetting; // Load settings context
        
        // 5. Override Transparency: Make component transparent
        // Values: true (1) to make transparent, false (0) to make solid
        int[] transparencyData = new int[] { 1 }; // 1 = Transparent
        ext.Transparency = dsSetting;

        // 6. Override Display Mode: Change component representation
        // Values: swDisplayMode_e (e.g. swHiddenLinesRemoved = 2, swShaded = 3)
        int[] displayModeData = new int[] { (int)swDisplayMode_e.swShaded };
        ext.DisplayMode = dsSetting;

        // Redraw graphics view
        model.GraphicsRedraw2();
    }
}
```

---

## 🎨 Applying Component Colors in Specific Display States

To change the optical appearance (color, shininess, transparency) of a component at the assembly level, use the `IComponent2.SetMaterialPropertyValues2` method.

### Material Property Array Indices (9 double values, range 0.0 to 1.0):
*   `[0]` — Red component color factor.
*   `[1]` — Green component color factor.
*   `[2]` — Blue component color factor.
*   `[3]` — Ambient light coefficient.
*   `[4]` — Diffuse light reflection coefficient.
*   `[5]` — Specular highlight intensity.
*   `[6]` — Shininess (specular spread).
*   `[7]` — Transparency coefficient (`0.0` = solid, `1.0` = invisible).
*   `[8]` — Emission light coefficient.

### C# Code: Applying Assembly Color Override
```csharp
public void ApplyComponentColor(IModelDoc2 model, IComponent2 component, byte r, byte g, byte b)
{
    // Define the 9 double values array
    double[] materialProps = new double[9];
    
    // RGB colors must be scaled between 0.0 and 1.0 (Value / 255.0)
    materialProps[0] = r / 255.0; // Red
    materialProps[1] = g / 255.0; // Green
    materialProps[2] = b / 255.0; // Blue
    
    // Surface properties
    materialProps[3] = 0.5; // Ambient
    materialProps[4] = 0.6; // Diffuse
    materialProps[5] = 0.4; // Specular
    materialProps[6] = 0.5; // Shininess
    materialProps[7] = 0.0; // Transparency (0 = solid)
    materialProps[8] = 0.0; // Emission

    // Apply color values to the component configuration
    // swInConfigurationOpts_e.swThisConfiguration (1) targets only the active assembly configuration
    component.SetMaterialPropertyValues2(
        materialProps, 
        (int)swInConfigurationOpts_e.swThisConfiguration, 
        null
    );

    // Redraw graphics
    model.GraphicsRedraw2();
}

public void ClearComponentColorOverride(IModelDoc2 model, IComponent2 component)
{
    // Passing null or an array of -1 values resets the component back to its default part color
    component.SetMaterialPropertyValues2(
        null, 
        (int)swInConfigurationOpts_e.swThisConfiguration, 
        null
    );
    
    model.GraphicsRedraw2();
}
```

---

## 🛠 Key Display State Methods & Properties

| Method / Property | Interface | Description |
| :--- | :--- | :--- |
| `GetDisplayStates()` | `IConfiguration` | Returns an array of strings representing available display states. |
| `ActiveDisplayState` | `IConfiguration` | Gets or sets the active display state name. |
| `GetDisplayStateSetting(...)` | `IModelDocExtension`| Instantiates an `IDisplayStateSetting` configuration context. |
| `Visibility` | `IModelDocExtension`| Gets or sets the visibility state of specified components. |
| `Transparency` | `IModelDocExtension`| Gets or sets the transparency state of specified components. |
| `DisplayMode` | `IModelDocExtension`| Gets or sets the display representation of specified components. |
| `SetMaterialPropertyValues2(...)`| `IComponent2` | Applies color, transparency, and specular values to a component. |

---

## ⚠️ Known Issues and Developer Rules

1.  **Component Appears Black after Color Override:**
    If a component turns solid black when applying properties via `SetMaterialPropertyValues2`, verify that the RGB double values are scaled between `0.0` and `1.0`. Passing integer values like `255` instead of `1.0` overflows the OpenGL shader bounds, resulting in black colors.
2.  **Display States Linked to Configurations:**
    SolidWorks configurations can have display states linked to them (`IConfiguration.LinkDisplayStatesToConfigurations`). When linked, a display state created in one configuration is not accessible inside another. Check the link status if `ActiveDisplayState` throws a COM exception when assigning names.
3.  **View Repaint Required:**
    Modifying visibility, transparency, or color overrides via API does not repaint the screen automatically. Always invoke `model.GraphicsRedraw2()` to refresh the active viewport display.

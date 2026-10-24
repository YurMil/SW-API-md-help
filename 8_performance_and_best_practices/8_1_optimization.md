# 8.1. Performance & Stability in SolidWorks API

When automating SolidWorks through external C# applications, program execution speed directly affects user experience. Without performance optimization, simple operations (such as updating assembly custom properties or exporting drawing formats) can cause significant delays due to repeated graphical window repaints and model rebuilds.

This section covers optimization methods for speeding up the SolidWorks API and safely releasing unmanaged COM resources.

---

## 🚀 Speeding Up SolidWorks API Execution

Three core methods are used to optimize execution speed:
1.  **Disabling Screen Updates:** Freezing the SolidWorks graphics canvas from repainting during long processes.
2.  **Deferred Rebuilds:** Turning off automatic model geometry rebuilds after adding individual features. Rebuild the model only once at the end of the operation.
3.  **Background Mode (Silent Open):** Opening documents invisibly without launching document windows in the GUI.

### Performance Benchmark Comparison:

| Execution Mode | Processing Time for 100 Assembly Parts |
| :--- | :--- |
| Standard Mode (Full graphics repaints & auto-rebuilds) | ~ 120 seconds |
| **Optimized Mode (Graphics frozen & deferred rebuilds)** | **~ 15 seconds (8x speed increase)** |

---

## 💻 Optimized C# Performance Helper Class

The class template below suspends resource-intensive updates when instantiated, and automatically restores them using `IDisposable` when execution leaves the block scope.

```csharp
using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksAutomation
{
    public class PerformanceScope : IDisposable
    {
        private readonly ISldWorks _swApp;
        private readonly IModelDoc2 _model;
        
        // Track original settings values for restoration
        private readonly bool _originalScreenUpdating;
        private readonly bool _originalEnableRebuild;

        public PerformanceScope(ISldWorks swApp, IModelDoc2 model = null)
        {
            _swApp = swApp;
            _model = model;

            // 1. Indicate external command processing to SolidWorks
            _swApp.CommandInProgress = true;

            // Retrieve and freeze screen updates
            _originalScreenUpdating = _swApp.GetSystemValue((int)swSysValues_e.swSystemValue_ScreenUpdating) == 1;
            _swApp.SetSystemValue((int)swSysValues_e.swSystemValue_ScreenUpdating, 0); // 0 = Disable updates

            // 2. Disable assembly-level rebuilds (if document context is an Assembly)
            if (_model != null && _model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                IAssemblyDoc asm = _model as IAssemblyDoc;
                if (asm != null)
                {
                    _originalEnableRebuild = asm.EnableAssemblyRebuild;
                    asm.EnableAssemblyRebuild = false; // Disable auto-rebuilds
                }
            }
        }

        /// <summary>
        /// Restore system preferences to original values on exit
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 1. Restore screen updates
                int restoreUpdating = _originalScreenUpdating ? 1 : 0;
                _swApp.SetSystemValue((int)swSysValues_e.swSystemValue_ScreenUpdating, restoreUpdating);

                // 2. Restore assembly rebuilds
                if (_model != null && _model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    IAssemblyDoc asm = _model as IAssemblyDoc;
                    if (asm != null)
                    {
                        asm.EnableAssemblyRebuild = _originalEnableRebuild;
                    }
                }

                // Force a single model rebuild
                if (_model != null)
                {
                    _model.ForceRebuild3(false);
                }
            }
            finally
            {
                // Release the command in progress flag
                _swApp.CommandInProgress = false;
            }
        }
    }
}
```

### Implementing the Performance Helper:

```csharp
public void ExecuteHeavyModifications(ISldWorks swApp, IModelDoc2 model)
{
    // The using statement guarantees restoration of preferences even if errors occur
    using (new PerformanceScope(swApp, model))
    {
        // Execute multiple modifications without graphical delays
        for (int i = 0; i < 100; i++)
        {
            // Edit parameters, write properties, create features...
        }
    } // Dispose() is invoked automatically here, rebuilding the model once
}
```

---

## 🧼 Safely Releasing COM Objects

Hanging instances of `sldworks.exe` in the Windows Task Manager occur because the .NET Garbage Collector does not track the lifecycle of unmanaged COM object reference counts.

### Key Rules for COM Memory Management:
1.  **Explicitly Release Every Reference:** If you hold a reference to an `IModelDoc2`, `IFeature`, `IView`, or `ISheet` object, release it using `Marshal.ReleaseComObject(obj)`.
2.  **Avoid Nested Dot Accessors:** Calling `string name = swApp.ActiveDoc.GetTitle();` creates an untracked intermediate COM reference to the document. You cannot release this reference, causing memory leaks.
    *   *Incorrect:* `swApp.ActiveDoc.GetTitle();`
    *   *Correct:*
        ```csharp
        IModelDoc2 doc = swApp.ActiveDoc as IModelDoc2;
        string title = doc.GetTitle();
        Marshal.ReleaseComObject(doc); // Safely release reference
        ```

### Safe File Lifecycle Implementation:

```csharp
public void SafeCloseDocument(ISldWorks swApp, string filePath)
{
    IModelDoc2 doc = null;
    try
    {
        int errors = 0;
        int warnings = 0;
        doc = swApp.OpenDoc6(filePath, 1, 1, "", ref errors, ref warnings);
        
        // Process document properties...
    }
    finally
    {
        if (doc != null)
        {
            // Close the document in SolidWorks
            swApp.CloseDoc(doc.GetTitle());
            
            // Release the COM reference
            Marshal.ReleaseComObject(doc);
            doc = null;
        }
    }
}
```

---

## 🛡 Handling COM HRESULT Exception Codes

Because the SolidWorks API is a COM server, failing operations (like selecting deleted entities or passing invalid dimension values) throw a `COMException`.

Always wrap SolidWorks API processes inside `try-catch` blocks and inspect HRESULT codes:

```csharp
try
{
    // SolidWorks API commands...
}
catch (COMException ex)
{
    int hResult = ex.ErrorCode;
    Console.WriteLine($"COM Exception caught: HRESULT = 0x{hResult:X}. Message: {ex.Message}");
}
```

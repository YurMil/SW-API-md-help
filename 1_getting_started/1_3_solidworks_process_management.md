# 1.3. SolidWorks Process Management & Anti-Ghosting (.NET 8 / 9 / 10)

When developing C# applications that automate SOLIDWORKS via the COM API (.NET 8, .NET 9, .NET 10), developers often encounter **ghost processes**. A ghost process is an orphaned `SLDWORKS.exe` process that stays running in the background in Windows Task Manager after the script finishes or crashes.

When ghost processes accumulate ("process stacking"), they consume system RAM, lock 3D model files (causing file access errors), and prevent new automation scripts from connecting to SOLIDWORKS.

---

## 1. Why Ghost Processes Occur

1. **Unreleased COM References (Runtime Callable Wrappers - RCW):**  
   In .NET, the Garbage Collector (GC) does not track unmanaged COM reference counts automatically. If your C# code keeps active references to `IModelDoc2`, `IView`, `IFeature`, or the root `ISldWorks` object, the SOLIDWORKS COM server cannot shut down even after the user closes the main window.
2. **UAC Elevation Mismatch (Standard User vs Administrator):**  
   If SOLIDWORKS is running under a standard user account, but your terminal or IDE (Visual Studio / Rider) runs **as Administrator** (or vice versa), Windows security blocks access to the Running Object Table (ROT). Because `GetActiveObject` fails, the script spawns a **new duplicate background process** on every run.
3. **Missing `Marshal.GetActiveObject` in Modern .NET (.NET Core / 8+):**  
   Calling the legacy method from .NET Framework 4.8 results in compilation errors or runtime exceptions.
4. **Hanging Modal Dialogs (User Prompts):**  
   The process waits for a user response (for example, "Save changes?", rebuild warnings, or the "Modify Dimension" value prompt) while running in invisible background mode.

---

## 2. Reliable Process Manager for .NET 8 / 9 / 10 (C#)

The following C# class provides a verified pattern for connecting to an existing instance or starting a new one safely:

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksAutomation
{
    public static class SwProcessManager
    {
        // Official SOLIDWORKS application CLSID
        private static readonly Guid SldWorksClsid = new("96749377-3391-11D2-9EE3-00C04F797396");

        // Native Win32 OLE call for modern .NET (replaces the removed Marshal.GetActiveObject)
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// Connects to a running SOLIDWORKS instance or starts a new one safely.
        /// </summary>
        public static ISldWorks ConnectOrStart(bool makeVisible = true)
        {
            // 1. Terminate orphaned background ghost processes from earlier runs
            KillGhostProcesses();

            ISldWorks? swApp = null;

            // 2. Try to connect to an active instance via ROT
            try
            {
                Guid clsid = SldWorksClsid;
                GetActiveObject(ref clsid, IntPtr.Zero, out object activeComObj);
                swApp = activeComObj as ISldWorks 
                    ?? throw new InvalidCastException("ROT object does not implement ISldWorks.");

                Console.WriteLine("Connected to running SOLIDWORKS instance.");
                return swApp;
            }
            catch (COMException)
            {
                Console.WriteLine("No active instance found in ROT.");

                // 3. Detect UAC privilege mismatch (Administrator vs Standard User)
                if (HasVisibleSolidWorksWindow())
                {
                    throw new InvalidOperationException(
                        "An open SOLIDWORKS window was found, but connection was blocked by Windows security. " +
                        "Run both your application and SOLIDWORKS with the same user privileges (both as standard user or both as Administrator).");
                }
            }

            // 4. If no running instance was found, start a new one via ProgID
            Console.WriteLine("Starting a new SOLIDWORKS process...");
            Type? swType = Type.GetTypeFromProgID("SldWorks.Application")
                ?? throw new InvalidOperationException("ProgID 'SldWorks.Application' is not registered on this system.");

            object created = Activator.CreateInstance(swType)
                ?? throw new InvalidOperationException("Failed to create SldWorks.Application instance.");

            swApp = created as ISldWorks
                ?? throw new InvalidCastException("Created object does not implement ISldWorks.");

            swApp.Visible = makeVisible;

            // 5. Wait for the COM message pump to be ready (prevents RPC_E_SERVERCALL_RETRYLATER 0x8001010A)
            WaitForMessagePump(swApp);

            Console.WriteLine($"SOLIDWORKS is ready. Revision: {swApp.RevisionNumber()}");
            return swApp;
        }

        /// <summary>
        /// Waits until the COM message pump is ready to handle calls.
        /// </summary>
        private static void WaitForMessagePump(ISldWorks swApp)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    _ = swApp.RevisionNumber();
                    return;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// Terminates only background ghost processes (processes without a main window title).
        /// Active user sessions with a visible window title are preserved.
        /// </summary>
        public static void KillGhostProcesses()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("SLDWORKS");
                foreach (Process p in processes)
                {
                    // A process is a ghost if it has no window title
                    if (string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        try
                        {
                            Console.WriteLine($"Terminating background ghost process (PID: {p.Id})...");
                            p.Kill();
                            p.WaitForExit(3000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not kill process {p.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning processes: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks whether SOLIDWORKS is currently running with a visible GUI window title.
        /// </summary>
        public static bool HasVisibleSolidWorksWindow()
        {
            Process[] processes = Process.GetProcessesByName("SLDWORKS");
            foreach (var p in processes)
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Releases the root COM reference cleanly and forces garbage collection.
        /// </summary>
        public static void SafeRelease(ref ISldWorks? swApp, bool closeApp = false)
        {
            if (swApp == null) return;

            try
            {
                if (closeApp)
                {
                    swApp.ExitApp();
                }

                Marshal.ReleaseComObject(swApp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning when releasing COM object: {ex.Message}");
            }
            finally
            {
                swApp = null;
                // Force collection of unreferenced RCW wrappers
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
}
```

---

## 3. Safe Shutdown Pattern

Always wrap automation workflows in `try...finally` to ensure the COM reference is released:

```csharp
ISldWorks? swApp = null;
try
{
    swApp = SwProcessManager.ConnectOrStart(makeVisible: true);
    
    // Core automation logic
    var activeDoc = swApp.IActiveDoc2;
    Console.WriteLine($"Active document: {activeDoc?.GetTitle() ?? "None"}");
}
catch (Exception ex)
{
    Console.WriteLine($"Execution error: {ex.Message}");
}
finally
{
    // Release COM references cleanly (keep the user's SOLIDWORKS window open)
    SwProcessManager.SafeRelease(ref swApp, closeApp: false);
}
```

---

## 4. Suppressing Dialog Prompts in Automated Workflows

During automated batch operations (such as opening files, adding dimensions, or exporting), pop-up modal dialogs can pause execution. Suppress these dialogs before running batch code:

```csharp
// 1. Suppress the "Modify Dimension" input pop-up when adding dimensions
bool prevDimPrompt = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate);
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);

// 2. Suppress the auto-recovery dialog prompt
bool prevAutoRecover = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog);
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog, false);

// 3. Set the external batch command flag
swApp.CommandInProgress = true;

try
{
    // ... Generate drawings or place dimensions ...
}
finally
{
    swApp.CommandInProgress = false;
    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, prevDimPrompt);
    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog, prevAutoRecover);
}
```

# 8.2. SOLIDWORKS Process Management & Stacking Prevention

In SOLIDWORKS API automation using C#, developer applications often suffer from **process stacking** (ghost `sldworks.exe` instances running in the background). This guide explains why this happens and how to prevent it.

---

## 🔍 Why SOLIDWORKS Processes Stack Up

There are two primary reasons why ghost `sldworks.exe` processes accumulate in the Windows Task Manager:

1. **UAC (User Account Control) Elevation Mismatch:**
   If SOLIDWORKS is running with standard user privileges but your IDE or terminal (where you run the C# script) is running as **Administrator**, Windows security blocks the Administrator process from connecting to the existing user's instance in the Running Object Table (ROT). Consequently, the script fails to attach and spawns a **new duplicate background process** on every execution.
2. **Leaked COM References (RCW):**
   C# interacts with SOLIDWORKS via Runtime Callable Wrappers (RCW). If you hold references to SOLIDWORKS objects (like `IModelDoc2`, `IView`, `IFeature`) and do not release them, or do not force Garbage Collection on exit, the COM reference count never drops to zero. Even if the user closes the SOLIDWORKS window, the process remains active as a ghost background task.

---

## 🛠 Best Practices to Prevent Process Stacking

### 1. Automatically Clean Up Ghost Background Processes
At the start of your script, search for any background `SLDWORKS` processes (processes that are running but have no visible main window title) and terminate them. Do **not** terminate processes that have a visible window title to avoid closing the user's unsaved work.

### 2. Detect UAC Elevation Mismatches
If a visible SOLIDWORKS window is already open but `GetActiveObject` fails, warn the user and terminate the script rather than spawning a duplicate background process.

### 3. Release COM References & Invoke GC on Exit
At the end of your program, call `Marshal.ReleaseComObject()` on the root `ISldWorks` reference and call `GC.Collect()` followed by `GC.WaitForPendingFinalizers()` to cleanly flush all RCW wrappers.

---

## 💻 C# Connection Template (Safe & Anti-Stacking)

The following template implements these best practices in a console or desktop application:

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

public class SolidWorksProcessManager
{
    private static readonly Guid SldWorksGuid = new Guid("96749377-3391-11D2-9EE3-00C04F797396");

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static ISldWorks Connect(out bool isNewInstance)
    {
        isNewInstance = false;
        ISldWorks swApp = null;

        // 1. Terminate orphaned ghost background processes from previous runs
        try
        {
            Process[] processes = Process.GetProcessesByName("SLDWORKS");
            foreach (var proc in processes)
            {
                // A background ghost process has no visible main window title
                if (string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    Console.WriteLine($"Killing background ghost SOLIDWORKS process (PID: {proc.Id})...");
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup warning: {ex.Message}");
        }

        // 2. Try to connect to an active running instance
        try
        {
            Guid clsid = SldWorksGuid;
            GetActiveObject(ref clsid, IntPtr.Zero, out object swAppObj);
            swApp = (ISldWorks)swAppObj;
            Console.WriteLine("Connected to active SOLIDWORKS instance.");
            return swApp;
        }
        catch
        {
            Console.WriteLine("Could not connect to active instance via ROT.");

            // 3. Detect UAC privilege mismatch
            try
            {
                Process[] processes = Process.GetProcessesByName("SLDWORKS");
                foreach (var proc in processes)
                {
                    if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                    {
                        Console.WriteLine($"WARNING: An active SOLIDWORKS window '{proc.MainWindowTitle}' is running (PID: {proc.Id}), but we cannot connect.");
                        Console.WriteLine("Please run both the terminal/IDE and SOLIDWORKS with the same user privileges (both as Admin or both as normal user).");
                        return null; // Terminate script to prevent process stacking
                    }
                }
            }
            catch {}

            // 4. Start a new instance if no active window is found
            Console.WriteLine("Starting a new SOLIDWORKS instance...");
            try
            {
                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                swApp = (ISldWorks)Activator.CreateInstance(swType);
                if (swApp != null)
                {
                    swApp.Visible = true;
                    isNewInstance = true;
                    Console.WriteLine("New SOLIDWORKS instance started.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start SOLIDWORKS: {ex.Message}");
            }
        }

        return swApp;
    }

    public static void Release(ISldWorks swApp, bool wasSpawnedByUs)
    {
        if (swApp != null)
        {
            try
            {
                if (wasSpawnedByUs)
                {
                    Console.WriteLine("Closing spawned SOLIDWORKS application...");
                    swApp.ExitApp();
                }
            }
            finally
            {
                Marshal.ReleaseComObject(swApp);
                swApp = null;
            }
        }

        // Force GC to clean up any remaining COM object wrappers in memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("COM resources released successfully.");
    }
}
```

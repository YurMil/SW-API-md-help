# 1.1. SolidWorks API Connection Guide: Modern C# & .NET 8 / 9 / 10

Connecting a C# application to the SolidWorks COM API (`sldworks.exe`) is the foundational step of any automation project. While classic tutorials rely on legacy .NET Framework 4.8 patterns, modern CAD automation targets **.NET 8 (LTS)**, **.NET 9**, and **.NET 10**.

This guide covers official connection patterns, SDK-style `.csproj` configurations, resolving COM interop changes across modern .NET runtimes, handling UAC elevation mismatches, and preventing orphaned background processes.

---

## 🎯 Architecture Models

There are two primary integration models for SolidWorks automation:

1. **Standalone Application (Out-of-Process Automation):**  
   Your program runs as a separate process (`.exe`), executing in its own memory space. It connects to an active SolidWorks process through the Windows **Running Object Table (ROT)** or launches a new `sldworks.exe` instance. Used for batch processors, CLI utilities, background services, and external desktop tools (WPF / WinForms / Avalonia / Console).
2. **Native Add-in (In-Process Automation):**  
   Your program compiles into a native `.dll` class library implementing `SolidWorks.Interop.swpublished.ISwAddin`. It registers in the Windows registry as a COM server and loads directly into the `sldworks.exe` address space, providing low-latency UI integrations, custom PropertyManager pages, and CommandManager toolbar tabs.

---

## ⚙️ Modern Project Setup (`.csproj`) for .NET 8, .NET 9 & .NET 10

Modern .NET uses SDK-style project files. Several critical compiler and runtime settings must be configured to ensure stable COM interop with SolidWorks:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <!-- Target modern .NET: .NET 8 (LTS), .NET 9, or .NET 10 -->
    <TargetFrameworks>net10.0-windows;net9.0-windows;net8.0-windows</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    
    <!-- CRITICAL 1: Platform MUST be x64. SOLIDWORKS is strictly a 64-bit COM server. -->
    <PlatformTarget>x64</PlatformTarget>
    <Platforms>x64</Platforms>

    <!-- CRITICAL 2: Disable Trimming for Single-File builds.
         COM RCW (Runtime Callable Wrapper) metadata relies on runtime reflection. -->
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>

  <!-- OPTION A (Recommended): Official NuGet Interop Packages -->
  <ItemGroup>
    <PackageReference Include="SolidWorks.Interop.sldworks" Version="32.1.0" />
    <PackageReference Include="SolidWorks.Interop.swconst" Version="32.1.0" />
    <PackageReference Include="SolidWorks.Interop.swpublished" Version="32.1.0" />
  </ItemGroup>

  <!-- OPTION B: Direct Local Assembly References (Alternative if not using NuGet)
       Assemblies are located in: C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\
  <ItemGroup>
    <Reference Include="SolidWorks.Interop.sldworks">
      <HintPath>C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll</HintPath>
      <EmbedInteropTypes>false</EmbedInteropTypes>
      <Private>false</Private>
    </Reference>
    <Reference Include="SolidWorks.Interop.swconst">
      <HintPath>C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll</HintPath>
      <EmbedInteropTypes>false</EmbedInteropTypes>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
  -->

</Project>
```

### Critical Compiler Settings:
* **`PlatformTarget = x64`:** SolidWorks has been exclusively 64-bit for over a decade. Attempting to build as `x86` or `AnyCPU` (running 32-bit) fails with `BadImageFormatException` or fails to locate the 64-bit Running Object Table entries.
* **`Embed Interop Types = False`:** Never embed interop types (`<EmbedInteropTypes>false</EmbedInteropTypes>`). When set to `True`, the C# compiler embeds partial type definitions into your assembly, which leads to `InvalidCastException` at runtime when passing COM interfaces between methods or assemblies.
* **`PublishTrimmed = false`:** In .NET 8 / 9 / 10, self-contained single-file publishing activates IL trimming by default. Because COM RCW relies on unmanaged vtable mappings and reflection, trimming can strip required metadata and crash your application at runtime.

---

## ⚡ The Modern .NET COM Dilemma (`Marshal.GetActiveObject`)

In legacy **.NET Framework 4.8**, attaching to a running instance was trivial:
```csharp
// Works ONLY in legacy .NET Framework 4.8; FAILS in .NET 6 / 8 / 9 / 10!
_swApp = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
```

> [!WARNING]
> **Breaking Change in .NET Core, .NET 5, 6, 7, 8, 9, 10:**  
> Microsoft deliberately removed `Marshal.GetActiveObject` from the Base Class Library (BCL) in `System.Runtime.InteropServices.Marshal`. Calling it will fail to compile or throw a `PlatformNotSupportedException`.

### The Solution: Direct Win32 OLEAUT32 P/Invoke
To connect to the active SolidWorks instance in modern .NET 8 / 9 / 10, call the native Windows OLE function `GetActiveObject` from `oleaut32.dll` using the official **SolidWorks CLSID**:
* **SOLIDWORKS Application CLSID:** `{96749377-3391-11D2-9EE3-00C04F797396}`

```csharp
[DllImport("oleaut32.dll", PreserveSig = false)]
private static extern void GetActiveObject(
    ref Guid rclsid,
    IntPtr pvReserved,
    [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
```

---

## 💻 Enterprise Production Connection Manager (.NET 8 / 9 / 10 Ready)

Below is an enterprise-grade, battle-tested connection template in C# 12 / 13 / 14. It addresses:
1. **Thread Apartment State (`[STAThread]`):** SOLIDWORKS COM requires Single-Threaded Apartment (STA).
2. **Ghost Process Cleanup:** Eliminates background orphaned `SLDWORKS.exe` instances left by crashed scripts.
3. **UAC Elevation Mismatch Detection:** Warns if an active user window exists but cannot be accessed due to privilege differences.
4. **Cold Start & Warm Attach:** Attaches to ROT or launches a new instance.
5. **Readiness Polling (`WaitForSolidWorksReady`):** Waits until the SolidWorks COM server message pump is fully initialized, preventing `RPC_E_SERVERCALL_RETRYLATER` (`0x8001010A`).
6. **Clean RCW Disposal & Memory Collection:** Flushes runtime callable wrappers on shutdown.

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksAutomation
{
    public sealed class SolidWorksConnectionResult
    {
        public ISldWorks App { get; init; } = null!;
        public bool IsNewInstance { get; init; }
        public string Version { get; init; } = string.Empty;
    }

    public static class SolidWorksConnectionManager
    {
        // Official SolidWorks Application CLSID
        private static readonly Guid SldWorksClsid = new("96749377-3391-11D2-9EE3-00C04F797396");

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// Connects to a running SolidWorks instance or launches a new one.
        /// Compatible with .NET 8, .NET 9, and .NET 10.
        /// </summary>
        public static SolidWorksConnectionResult Connect(bool makeVisible = true, Action<string>? logger = null)
        {
            void Log(string message) => logger?.Invoke($"[SolidWorksConnection] {message}");

            // 1. Clean up orphaned background ghost processes from previous interrupted sessions
            KillGhostBackgroundProcesses(Log);

            ISldWorks? swApp = null;
            bool isNewInstance = false;

            // 2. Try to connect to an active running instance via Windows ROT
            try
            {
                Guid clsid = SldWorksClsid;
                GetActiveObject(ref clsid, IntPtr.Zero, out object activeComObject);
                swApp = activeComObject as ISldWorks 
                    ?? throw new InvalidCastException("Connected COM object does not implement ISldWorks.");

                Log("Successfully attached to active running SolidWorks instance.");
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x800401E3)) // MK_E_UNAVAILABLE
            {
                Log("No active SolidWorks instance found in ROT.");

                // 3. Check for UAC privilege mismatch before launching a new process
                if (HasVisibleSolidWorksWindow())
                {
                    throw new InvalidOperationException(
                        "A visible SOLIDWORKS window is running, but the automation process could not connect to it. " +
                        "This typically indicates a UAC privilege elevation mismatch. " +
                        "Ensure both your application and SOLIDWORKS are running with identical privilege levels " +
                        "(both as Standard User or both Elevated as Administrator).");
                }
            }

            // 4. If no running instance was attached, launch a new one
            if (swApp == null)
            {
                Log("Starting a new SOLIDWORKS instance via ProgID...");

                Type? swType = Type.GetTypeFromProgID("SldWorks.Application")
                    ?? throw new InvalidOperationException("SolidWorks COM ProgID 'SldWorks.Application' is not registered.");

                object createdObj = Activator.CreateInstance(swType)
                    ?? throw new InvalidOperationException("Failed to instantiate 'SldWorks.Application' COM server.");

                swApp = createdObj as ISldWorks
                    ?? throw new InvalidCastException("Created object does not implement ISldWorks.");

                swApp.Visible = makeVisible;
                isNewInstance = true;

                Log("Process spawned. Waiting for COM message pump initialization...");
                WaitForSolidWorksReady(swApp, Log);
            }

            string version = swApp.RevisionNumber();
            Log($"Connected successfully. SolidWorks Internal Revision: {version}");

            return new SolidWorksConnectionResult
            {
                App = swApp,
                IsNewInstance = isNewInstance,
                Version = version
            };
        }

        /// <summary>
        /// Polls the SolidWorks COM interface until it responds without RPC delay errors.
        /// Prevents RPC_E_SERVERCALL_RETRYLATER (0x8001010A).
        /// </summary>
        private static void WaitForSolidWorksReady(ISldWorks swApp, Action<string> log)
        {
            const int maxRetries = 25;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Access a lightweight property to test if message pump is ready
                    _ = swApp.RevisionNumber();
                    return;
                }
                catch (COMException)
                {
                    Thread.Sleep(400);
                }
            }

            log("Warning: SolidWorks process launched, but did not respond within the expected timeout.");
        }

        /// <summary>
        /// Terminates background ghost sldworks.exe processes that have no visible main window title.
        /// Preserves instances with active window titles (user sessions).
        /// </summary>
        public static void KillGhostBackgroundProcesses(Action<string> log)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("SLDWORKS");
                foreach (Process proc in processes)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(proc.MainWindowTitle))
                        {
                            log($"Terminating orphaned background ghost process (PID: {proc.Id})...");
                            proc.Kill();
                            proc.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"Failed to terminate process {proc.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"Process scanner exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if any SOLIDWORKS process currently has a visible GUI window title.
        /// </summary>
        public static bool HasVisibleSolidWorksWindow()
        {
            Process[] processes = Process.GetProcessesByName("SLDWORKS");
            foreach (var proc in processes)
            {
                if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Safely releases the ISldWorks RCW COM reference and enforces garbage collection.
        /// </summary>
        public static void Disconnect(ref ISldWorks? swApp, bool closeProcess = false, Action<string>? logger = null)
        {
            if (swApp == null) return;

            try
            {
                if (closeProcess)
                {
                    logger?.Invoke("Closing SolidWorks application...");
                    swApp.ExitApp();
                }

                Marshal.ReleaseComObject(swApp);
                logger?.Invoke("SolidWorks COM reference released successfully.");
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error releasing COM reference: {ex.Message}");
            }
            finally
            {
                swApp = null;
                // Force RCW finalization
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
}
```

---

## 🚀 Usage in Modern .NET Console or Desktop Application

Make sure your entry point has the `[STAThread]` attribute, which is required for Windows COM message dispatching:

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorksAutomation;

internal static class Program
{
    [STAThread] // Mandatory for SOLIDWORKS COM automation
    private static void Main(string[] args)
    {
        ISldWorks? swApp = null;

        try
        {
            Console.WriteLine("Connecting to SolidWorks (.NET 8 / 9 / 10)...");
            var connection = SolidWorksConnectionManager.Connect(
                makeVisible: true, 
                logger: Console.WriteLine);

            swApp = connection.App;

            // Perform automation tasks
            Console.WriteLine($"Active Doc: {swApp.IActiveDoc2?.GetTitle() ?? "None"}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Connection failed: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            // Clean up COM references without killing user's active session
            SolidWorksConnectionManager.Disconnect(ref swApp, closeProcess: false, logger: Console.WriteLine);
        }
    }
}
```

---

## 🎯 Targeting Specific SolidWorks Versions

In environments with multiple SOLIDWORKS versions installed side-by-side (e.g. 2024 and 2025), you can target a specific release using version-specific ProgIDs or by enumerating the Running Object Table (ROT).

### 1. Version-Specific ProgIDs

| SolidWorks Release | Internal Year / Version | Specific ProgID | Primary Interop Assembly (PIA) |
| :--- | :--- | :--- | :--- |
| **SOLIDWORKS 2022** | SW 2022 / Ver 30 | `SldWorks.Application.30` | 30.X.X |
| **SOLIDWORKS 2023** | SW 2023 / Ver 31 | `SldWorks.Application.31` | 31.X.X |
| **SOLIDWORKS 2024** | SW 2024 / Ver 32 | `SldWorks.Application.32` | 32.X.X |
| **SOLIDWORKS 2025** | SW 2025 / Ver 33 | `SldWorks.Application.33` | 33.X.X |
| **SOLIDWORKS 2026** | SW 2026 / Ver 34 | `SldWorks.Application.34` | 34.X.X |

To launch a specific version:
```csharp
Type? swType = Type.GetTypeFromProgID("SldWorks.Application.32"); // Forces SOLIDWORKS 2024
ISldWorks swApp = (ISldWorks)Activator.CreateInstance(swType)!;
```

---

## 🛠 Essential Methods and Diagnostic Properties of `ISldWorks`

| Method / Property | Return Type | Description |
| :--- | :--- | :--- |
| `Visible` | `bool` | Gets or sets whether the SolidWorks main frame is visible. |
| `RevisionNumber()` | `string` | Returns the internal release build (e.g., `"32.1.0"` for SW 2024 SP1). |
| `CommandInProgress` | `bool` | Set to `true` during long batch operations to suppress certain GUI warnings. |
| `UserControl` | `bool` | Determines whether the SolidWorks process remains alive after releasing all COM references. Set to `true` if you want the application to stay open for the user. |
| `IActiveDoc2` | `IModelDoc2` | Returns the currently active document, or `null` if no document is open. |
| `ExitApp()` | `void` | Requests a clean shutdown of the SolidWorks CAD application. |
| `SendMsgToUser2(...)` | `int` | Displays a native SolidWorks message box (`swMessageBoxIcon_e`, `swMessageBoxResult_e`). |

---

## ⚠️ Pitfalls, Error Codes & Troubleshooting

### 1. `PlatformNotSupportedException` on `Marshal.GetActiveObject`
* **Cause:** Running on .NET Core, .NET 5, 6, 7, 8, 9, or 10. `Marshal.GetActiveObject` is not part of modern .NET.
* **Fix:** Use the Win32 `oleaut32.dll` P/Invoke implementation demonstrated above.

### 2. `InvalidCastException` (`Unable to cast COM object of type 'SldWorksClass'`)
* **Cause:** The project has **Embed Interop Types** enabled (`<EmbedInteropTypes>true</EmbedInteropTypes>`), or code is casting to concrete classes (`SldWorksClass`) instead of interface contracts (`ISldWorks`).
* **Fix:** Always set `<EmbedInteropTypes>false</EmbedInteropTypes>` in your `.csproj` and always cast to `ISldWorks`, `IModelDoc2`, `IPartDoc`.

### 3. `0x800401E3 (MK_E_UNAVAILABLE)`
* **Cause:** `GetActiveObject` could not find an entry in the Windows ROT. Either SolidWorks is not running, or your program and SolidWorks are running under different integrity levels (UAC elevation mismatch).
* **Fix:** Ensure both programs run as Standard User or both run Elevated as Administrator.

### 4. `0x8001010A (RPC_E_SERVERCALL_RETRYLATER)`
* **Cause:** Calling SolidWorks API methods while the process is busy starting up, saving, or displaying a modal dialog.
* **Fix:** Implement a readiness wait loop (as in `WaitForSolidWorksReady`) or implement a COM `IOleMessageFilter` to handle `SERVERCALL_RETRYLATER` automatically.

### 5. Multi-Threading and MTA Crashes
* **Cause:** SolidWorks is an STA (Single-Threaded Apartment) COM server. Calling API methods from worker threads (`Task.Run`, background threads) without marshaling to an STA thread can cause unpredictable access violations and deadlocks.
* **Fix:** Always mark your UI/CLI entry point with `[STAThread]`. For background processing in WPF/WinForms, dispatch SolidWorks calls to a dedicated STA thread.

### 6. Background Orphaned `SLDWORKS.exe` Instances
* **Cause:** Failing to call `Marshal.ReleaseComObject()` on COM references or not calling `GC.Collect()`. The COM reference count remains non-zero, keeping the process alive in Task Manager.
* **Fix:** Wrap connections in `try...finally` blocks, invoke `KillGhostBackgroundProcesses` on startup, and trigger `GC.Collect()` after releasing.

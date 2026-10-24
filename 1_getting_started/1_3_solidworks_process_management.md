# 1.3. Управление процессами SOLIDWORKS и устранение «призрачных» экземпляров (.NET 8 / 9 / 10)

При разработке приложений, автоматизирующих SOLIDWORKS через COM API на C# (.NET 8, .NET 9, .NET 10), разработчики часто сталкиваются с проблемой **«призрачных процессов» (ghost processes)** — когда после завершения или аварийного прерывания скрипта в Диспетчере задач Windows накапливаются висящие процессы `SLDWORKS.exe`.

Накопление таких процессов («расслоение» или stacking) приводит к исчерпанию оперативной памяти, взаимоблокировкам файлов моделей (ошибки доступа при повторном открытии) и невозможности подключиться к САПР.

---

## 1. Основные причины расслоения и зависания процессов

1. **Неосвобожденные COM-ссылки (Runtime Callable Wrappers — RCW):**  
   В среде .NET сборщик мусора (GC) не отслеживает внутренний счетчик ссылок COM-сервера. Если в C# удерживаются ссылки на `IModelDoc2`, `IView`, `IFeature` или корневой `ISldWorks`, процесс SOLIDWORKS не сможет завершиться даже после закрытия окна пользователем.
2. **Несовпадение прав UAC (Integrity Level Mismatch):**  
   Если SOLIDWORKS запущен от имени обычного пользователя, а IDE (Visual Studio, Rider) или консоль запущена **«От имени Администратора»** (или наоборот), Windows блокирует чтение таблицы активных объектов (ROT). В результате вызов `GetActiveObject` завершается ошибкой, и скрипт каждый раз создает **новый фоновый дубликат** процесса.
3. **Отсутствие метода `Marshal.GetActiveObject` в .NET Core / .NET 8+:**  
   Попытка использовать устаревший вызов из .NET Framework 4.8 приводит к ошибке компиляции или падению в рантайме.
4. **Зависшие диалоговые окна (User Prompts):**  
   Процесс ждет ответа пользователя (например, запрос на сохранение изменений, предупреждение о перестроении или окно ввода значения размера), работая в невидимом фоновом режиме.

---

## 2. Надежный менеджер процессов для .NET 8 / 9 / 10 (C#)

Ниже представлен проверенный enterprise-шаблон подключения и контроля жизненного цикла, готовый к компиляции в современных версиях .NET:

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
        // Официальный CLSID приложения SOLIDWORKS
        private static readonly Guid SldWorksClsid = new("96749377-3391-11D2-9EE3-00C04F797396");

        // Прямой P/Invoke вызов Win32 OLE для работы в современном .NET (вместо удаленного Marshal.GetActiveObject)
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// Подключается к работающему SOLIDWORKS или безопасно запускает новый экземпляр.
        /// </summary>
        public static ISldWorks ConnectOrStart(bool makeVisible = true)
        {
            // 1. Устраняем зависшие невидимые фоновые процессы от предыдущих сессий
            KillGhostProcesses();

            ISldWorks? swApp = null;

            // 2. Пробуем подключиться к активному экземпляру через ROT
            try
            {
                Guid clsid = SldWorksClsid;
                GetActiveObject(ref clsid, IntPtr.Zero, out object activeComObj);
                swApp = activeComObj as ISldWorks 
                    ?? throw new InvalidCastException("Объект из ROT не реализует интерфейс ISldWorks.");

                Console.WriteLine("Успешно подключено к работающему экземпляру SOLIDWORKS.");
                return swApp;
            }
            catch (COMException)
            {
                Console.WriteLine("Активный экземпляр в ROT не найден.");

                // 3. Проверка на несовпадение прав UAC (Administrator vs Standard User)
                if (HasVisibleSolidWorksWindow())
                {
                    throw new InvalidOperationException(
                        "Обнаружено открытое окно SOLIDWORKS, но подключение заблокировано. " +
                        "Запустите ваше приложение и SOLIDWORKS с одинаковыми правами доступа (оба без прав админа или оба от администратора).");
                }
            }

            // 4. Если активных процессов нет, запускаем новый экземпляр через ProgID
            Console.WriteLine("Запуск нового процесса SOLIDWORKS...");
            Type? swType = Type.GetTypeFromProgID("SldWorks.Application")
                ?? throw new InvalidOperationException("ProgID 'SldWorks.Application' не зарегистрирован в системе.");

            object created = Activator.CreateInstance(swType)
                ?? throw new InvalidOperationException("Не удалось создать экземпляр SldWorks.Application.");

            swApp = created as ISldWorks
                ?? throw new InvalidCastException("Созданный объект не реализует ISldWorks.");

            swApp.Visible = makeVisible;

            // 5. Ожидаем готовности COM-интерфейса (предотвращает ошибку RPC_E_SERVERCALL_RETRYLATER 0x8001010A)
            WaitForMessagePump(swApp);

            Console.WriteLine($"SOLIDWORKS запущен и готов к работе. Версия: {swApp.RevisionNumber()}");
            return swApp;
        }

        /// <summary>
        /// Ожидает полной инициализации очереди сообщений COM-сервера.
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
        /// Завершает исключительно фоновые «призрачные» процессы (без заголовка окна),
        /// не затрагивая окна с активной работой пользователя.
        /// </summary>
        public static void KillGhostProcesses()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("SLDWORKS");
                foreach (Process p in processes)
                {
                    // Процесс считается фоновым "призраком", если у него нет заголовка главного окна
                    if (string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        try
                        {
                            Console.WriteLine($"Завершение фонового процесса-призрака (PID: {p.Id})...");
                            p.Kill();
                            p.WaitForExit(3000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Не удалось завершить процесс {p.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сканирования процессов: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверяет, запущен ли SOLIDWORKS с видимым графическим интерфейсом.
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
        /// Корректно закрывает ссылку на COM-объект и принудительно вызывает GC.
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
                Console.WriteLine($"Предупреждение при освобождении COM-объекта: {ex.Message}");
            }
            finally
            {
                swApp = null;
                // Принудительный сбор нереференцированных RCW
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
}
```

---

## 3. Правильное завершение скрипта и освобождение памяти

Чтобы процесс не зависал в памяти, корневой блок приложения должен оборачивать работу в `try...finally`:

```csharp
ISldWorks? swApp = null;
try
{
    swApp = SwProcessManager.ConnectOrStart(makeVisible: true);
    
    // Основная бизнес-логика автоматизации
    var activeDoc = swApp.IActiveDoc2;
    Console.WriteLine($"Текущий документ: {activeDoc?.GetTitle() ?? "нет"}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка выполнения: {ex.Message}");
}
finally
{
    // Безопасное освобождение ссылки (без завершения процесса пользователя)
    SwProcessManager.SafeRelease(ref swApp, closeApp: false);
}
```

---

## 4. Подавление всплывающих диалоговых окон при автоматизации

В процессе автоматизации (открытие файлов, простановка размеров, экспорт) всплывающие модальные окна могут полностью заблокировать скрипт. Рекомендуется отключать их на время пакетной обработки:

```csharp
// 1. Подавление окна ввода значения размера при его создании
bool prevDimPrompt = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate);
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);

// 2. Подавление диалога автовосстановления (Auto-Recover)
bool prevAutoRecover = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog);
swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog, false);

// 3. Установка флага пакетной команды
swApp.CommandInProgress = true;

try
{
    // ... Выполнение генерации чертежей / простановки размеров ...
}
finally
{
    swApp.CommandInProgress = false;
    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, prevDimPrompt);
    swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swShowAutoRecoverDialog, prevAutoRecover);
}
```

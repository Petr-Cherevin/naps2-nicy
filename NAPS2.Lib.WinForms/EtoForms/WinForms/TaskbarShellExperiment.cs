using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Explicit ITaskbarList test ONLY for /TaskbarTest=shell. No foreground calls.
/// </summary>
internal static class TaskbarShellExperiment
{
    [ComImport]
    [Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        [PreserveSig] int HrInit();
        [PreserveSig] int AddTab(nint hwnd);
        [PreserveSig] int DeleteTab(nint hwnd);
        [PreserveSig] int ActivateTab(nint hwnd);
        [PreserveSig] int SetActiveAlt(nint hwnd);
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    private class TaskbarListComClass { }

    private static int CallShell(nint hwnd, bool add)
    {
        var taskbar = (ITaskbarList)new TaskbarListComClass();
        try
        {
            int hr = taskbar.HrInit();
            if (hr < 0) return hr;
            return add ? taskbar.AddTab(hwnd) : taskbar.DeleteTab(hwnd);
        }
        finally
        {
            Marshal.ReleaseComObject(taskbar);
        }
    }

    public static void Attach(Form form)
    {
        bool registered = false;
        nint addedHandle = 0;
        form.Shown += (_, _) =>
        {
            // Defer until WinForms has completed the initial Show.
            form.BeginInvoke((Action)(() =>
            {
                if (form.IsDisposed || !form.IsHandleCreated || registered) return;
                try
                {
                    addedHandle = form.Handle;
                    int hr = CallShell(addedHandle, true);
                    registered = hr >= 0;
                    DiagnosticLog.Write("TaskbarShell",
                        $"action=AddTab hwnd=0x{addedHandle.ToInt64():X} hr=0x{hr:X8} registered={registered}");
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write("TaskbarShell", $"action=AddTab error={ex.GetType().Name}");
                }
            }));
        };
        form.FormClosing += (_, _) =>
        {
            if (!registered || addedHandle == 0) return;
            try
            {
                int hr = CallShell(addedHandle, false);
                DiagnosticLog.Write("TaskbarShell",
                    $"action=DeleteTab hwnd=0x{addedHandle.ToInt64():X} hr=0x{hr:X8}");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("TaskbarShell", $"action=DeleteTab error={ex.GetType().Name}");
            }
            registered = false;
        };
    }
}

using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Keep the NAPS2 main window registered with the Windows taskbar without
/// changing the foreground window. The initial AddTab follows the successfully
/// tested ChatGPT-0008 /TaskbarTest=shell sequence.
/// </summary>
internal sealed class TaskbarRegistration
{
    private readonly Form _form;
    private readonly TaskbarMessageHook _messageHook;
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 1000 };
    private bool _shown;
    private bool _closed;
    private bool _queued;
    private nint _registeredWindow;
    private nint _registeredTaskbar;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);

    private static readonly uint TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    private sealed class TaskbarMessageHook(Action onTaskbarCreated) : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == TaskbarCreatedMessage)
                onTaskbarCreated();
            base.WndProc(ref m);
        }
    }

    public static void Attach(Form form) => _ = new TaskbarRegistration(form);

    private TaskbarRegistration(Form form)
    {
        _form = form;
        _messageHook = new TaskbarMessageHook(OnTaskbarCreated);
        _retryTimer.Tick += (_, _) => TryRegister("retry");

        _form.HandleCreated += (_, _) =>
        {
            if (_closed) return;
            if (_messageHook.Handle != 0) _messageHook.ReleaseHandle();
            _messageHook.AssignHandle(_form.Handle);
            if (_shown) QueueRegister("handle-recreated");
        };
        _form.HandleDestroyed += (_, _) =>
        {
            // Removing the old HWND prevents a stale entry on handle recreation.
            Unregister("handle-destroyed");
            if (_messageHook.Handle != 0) _messageHook.ReleaseHandle();
        };
        _form.Shown += (_, _) =>
        {
            _shown = true;
            // Preserve the working 0008 timing: queue AddTab after Show completes.
            QueueRegister("initial-show");
        };
        _form.FormClosed += (_, _) =>
        {
            _closed = true;
            _retryTimer.Stop();
            _retryTimer.Dispose();
            Unregister("form-closed");
            if (_messageHook.Handle != 0) _messageHook.ReleaseHandle();
        };
        if (_form.IsHandleCreated)
            _messageHook.AssignHandle(_form.Handle);
    }

    private void OnTaskbarCreated()
    {
        // Explorer restart (or primary-display DPI change): the Shell may have
        // discarded previously added taskbar entries.
        _registeredWindow = 0;
        _registeredTaskbar = 0;
        DiagnosticLog.Write("Taskbar", "event=TaskbarCreated reRegister=True");
        if (_shown) QueueRegister("taskbar-created");
    }

    private void QueueRegister(string reason)
    {
        if (_closed || _queued || !_form.IsHandleCreated) return;
        _queued = true;
        try
        {
            _form.BeginInvoke((Action)(() =>
            {
                _queued = false;
                TryRegister(reason);
            }));
        }
        catch (InvalidOperationException)
        {
            _queued = false;
            _retryTimer.Start();
        }
    }

    private void TryRegister(string reason)
    {
        if (_closed || _form.IsDisposed || !_form.IsHandleCreated || !_form.Visible)
            return;

        nint hwnd = _form.Handle;
        nint taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (_registeredWindow == hwnd && _registeredTaskbar == taskbarHwnd && taskbarHwnd != 0)
        {
            _retryTimer.Stop();
            return;
        }
        if (taskbarHwnd == 0)
        {
            _retryTimer.Start();
            return;
        }

        try
        {
            int hr = CallShell(hwnd, add: true);
            DiagnosticLog.Write("Taskbar",
                $"action=AddTab reason={reason} hwnd=0x{hwnd.ToInt64():X} hr=0x{hr:X8}");
            if (hr >= 0)
            {
                _registeredWindow = hwnd;
                _registeredTaskbar = taskbarHwnd;
                _retryTimer.Stop();
            }
            else
            {
                _retryTimer.Start();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Taskbar", $"action=AddTab error={ex.GetType().Name}");
            _retryTimer.Start();
        }
    }

    private void Unregister(string reason)
    {
        nint hwnd = _registeredWindow;
        _registeredWindow = 0;
        _registeredTaskbar = 0;
        _retryTimer.Stop();
        if (hwnd == 0) return;

        try
        {
            int hr = CallShell(hwnd, add: false);
            DiagnosticLog.Write("Taskbar",
                $"action=DeleteTab reason={reason} hwnd=0x{hwnd.ToInt64():X} hr=0x{hr:X8}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Taskbar", $"action=DeleteTab error={ex.GetType().Name}");
        }
    }

    private static int CallShell(nint hwnd, bool add)
    {
        var taskbar = (ITaskbarList)new TaskbarListComClass();
        try
        {
            int hr = taskbar.HrInit();
            return hr < 0 ? hr : add ? taskbar.AddTab(hwnd) : taskbar.DeleteTab(hwnd);
        }
        finally
        {
            Marshal.ReleaseComObject(taskbar);
        }
    }
}

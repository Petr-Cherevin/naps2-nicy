namespace NAPS2.EtoForms;

/// <summary>
/// Explicit, test-only Windows taskbar startup variants. None persists in the user configuration.
/// </summary>
public enum TaskbarStartupMode
{
    Normal,
    Delayed,
    NoActivate,
    Shell
}

public static class TaskbarStartupTest
{
    public static TaskbarStartupMode Mode { get; private set; } = TaskbarStartupMode.Normal;

    public static void Configure(string[] args)
    {
        Mode = TaskbarStartupMode.Normal;
        var flag = args.FirstOrDefault(arg =>
            arg.StartsWith("/TaskbarTest=", StringComparison.OrdinalIgnoreCase));
        if (flag != null)
        {
            string value = flag["/TaskbarTest=".Length..].ToLowerInvariant();
            Mode = value switch
            {
                "normal" => TaskbarStartupMode.Normal,
                "delayed" => TaskbarStartupMode.Delayed,
                "noactivate" => TaskbarStartupMode.NoActivate,
                "shell" => TaskbarStartupMode.Shell,
                _ => TaskbarStartupMode.Normal
            };
            if (value is not ("normal" or "delayed" or "noactivate" or "shell"))
                DiagnosticLog.Write("TaskbarExperiment", $"unknown-mode={value} fallback=normal");
        }
        if (!OperatingSystem.IsWindows() && Mode != TaskbarStartupMode.Normal)
        {
            DiagnosticLog.Write("TaskbarExperiment", $"mode={Mode} ignored=non-windows");
            Mode = TaskbarStartupMode.Normal;
        }
        DiagnosticLog.Write("TaskbarExperiment",
            $"mode={Mode.ToString().ToLowerInvariant()} argSupplied={flag != null} pid={Environment.ProcessId}");
    }
}

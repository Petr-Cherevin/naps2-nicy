using Autofac;
using NAPS2.EtoForms;
using NAPS2.EtoForms.Ui;
using NAPS2.Modules;
using NAPS2.Remoting.Worker;
using NAPS2.Scan;

namespace NAPS2.EntryPoints;

/// <summary>
/// The entry point for the main NAPS2 executable.
/// </summary>
public static class GuiEntryPoint
{
    public static int Run(string[] args, Module imageModule, Module platformModule)
    {
        // Select a startup experiment before constructing the GUI.
        TaskbarStartupTest.Configure(args);

        // Initialize Autofac (the DI framework)
        var container = AutoFacHelper.FromModules(
            new CommonModule(), imageModule, platformModule, new GuiModule(), new RecoveryModule(), new StaticInitModule());

        Paths.ClearTemp();

        // Parse the command-line arguments and see if we're doing something other than displaying the main form
        var lifecycle = container.Resolve<ApplicationLifecycle>();
        lifecycle.ParseArgs(args);
        lifecycle.ExitIfRedundant();

        EtoPlatform.Current.InitializeForegroundApp();

        // Set up basic application configuration
        container.Resolve<CultureHelper>().SetCulturesFromConfig();
        Trace.Listeners.Add(new ConsoleTraceListener());

        // Start a pending worker process
        container.Resolve<IWorkerFactory>().Init(container.Resolve<ScanningContext>());

        // Show the main form
        var application = EtoPlatform.Current.CreateApplication();
        Invoker.Current = new EtoInvoker(application);
        var formFactory = container.Resolve<IFormFactory>();
        var desktop = formFactory.Create<DesktopForm>();

        if (TaskbarStartupTest.Mode == TaskbarStartupMode.NoActivate)
        {
            // Let the initial Show use the native ShowWithoutActivation path.
            desktop.ShowActivated = false;
            DiagnosticLog.Write("TaskbarExperiment", "initial-show-activation=disabled");
        }

        if (TaskbarStartupTest.Mode == TaskbarStartupMode.Delayed)
        {
            // Eto.Run(desktop) normally calls Show before WinForms.Application.Run.
            // Post Show to the UI message loop instead of showing synchronously.
            application.Initialized += (_, _) =>
            {
                DiagnosticLog.Write("TaskbarExperiment", "message-loop-startup=queued-show");
                application.AsyncInvoke(() =>
                {
                    DiagnosticLog.Write("TaskbarExperiment", "message-loop-running=show-start");
                    application.MainForm = desktop;
                    desktop.Show();
                    DiagnosticLog.Write("TaskbarExperiment", "message-loop-running=show-complete");
                });
            };
            application.Run();
        }
        else
        {
            DiagnosticLog.Write("TaskbarExperiment", "eto-run=standard");
            application.Run(desktop);
        }
        return 0;
    }
}
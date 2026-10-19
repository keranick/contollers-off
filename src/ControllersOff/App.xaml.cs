using System.IO;
using System.Text.Json;
using System.Windows;
using ControllersOff.Domain;
using ControllersOff.Devices;
using ControllersOff.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;
using ApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;

namespace ControllersOff;

public partial class App : Application
{
    private static readonly Mutex SingleInstance = new(initiallyOwned: true, "ControllersOff.SingleInstance", out _);
    private static readonly EventWaitHandle ShowSignal = new(false, EventResetMode.AutoReset, "ControllersOff.Show");

    private static bool _ownsSingleInstance;

    private TrayIconHost? _tray;
    private MainWindow? _window;
    private AppSettings _settings = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Commands must run before the single-instance check, otherwise the elevated helper never gets to work.
        if (TryRunCommandLine(e.Args, out var commandSucceeded))
        {
            Environment.ExitCode = commandSucceeded ? 0 : 1;
            Shutdown();
            return;
        }

        if (!SingleInstance.WaitOne(TimeSpan.Zero))
        {
            ShowSignal.Set();
            Shutdown();
            return;
        }

        _ownsSingleInstance = true;

        base.OnStartup(e);

        _settings = SettingsStore.Load();
        Loc.Instance.UseLanguage(_settings.Language);
        ApplyTheme(_settings.Theme);

        RecoverStrandedDevices();

        _window = new MainWindow(_settings);
        _tray = new TrayIconHost(() => _window!);

        WatchForSecondInstance(_window);

        if (Array.IndexOf(e.Args, "--tray") < 0)
        {
            _window.ShowFlyout();
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        var notRestored = _window?.RestoreIfNeeded() ?? [];
        if (notRestored.Count > 0)
        {
            System.Windows.MessageBox.Show(Loc.Instance["RestoreFailed"], Loc.Instance["AppTitle"]);
        }

        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }

        if (_ownsSingleInstance)
        {
            SingleInstance.ReleaseMutex();
        }

        base.OnExit(e);
    }

    // A previous session can end with devices still switched off, so they are turned back on before anything else.
    private static void RecoverStrandedDevices()
    {
        var stranded = DisabledDeviceLog.Load();
        if (stranded.Length == 0)
        {
            return;
        }

        if (RunDeviceCommand(["--toggle-all", string.Join(',', stranded), "on"]))
        {
            DisabledDeviceLog.Save([]);
        }
    }

    private static void WatchForSecondInstance(MainWindow window)
        => _ = Task.Run(() =>
        {
            while (ShowSignal.WaitOne())
            {
                Current.Dispatcher.Invoke(window.ShowFlyout);
            }
        });

    public static bool RunDeviceCommand(string[] arguments)
    {
        Trace.Write($"command [{string.Join(' ', arguments)}] admin={Elevation.IsAdministrator}");

        return Elevation.IsAdministrator ? DeviceCommands.Execute(arguments) : Elevation.RunElevated(arguments);
    }

    public static void ApplyTheme(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
        {
            ApplicationThemeManager.ApplySystemTheme();
            return;
        }

        ApplicationThemeManager.Apply(
            mode == ThemeMode.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.None,
            true);
    }

    private static bool TryRunCommandLine(string[] arguments, out bool succeeded)
    {
        succeeded = false;

        if (arguments.Length == 0)
        {
            return false;
        }

        switch (arguments[0])
        {
            case "--make-icon" when arguments.Length == 2:
                Tools.IconWriter.Write(arguments[1]);
                succeeded = true;
                return true;

            case "--dump":
                var dumpProvider = new SetupApiDeviceProvider();
                WriteDiagnostic(arguments.Length > 1 ? arguments[1] : "devices.json", dumpProvider, dumpProvider.Enumerate());
                succeeded = true;
                return true;

            case "--toggle" when arguments.Length == 3:
            case "--toggle-all" when arguments.Length == 3:
            case "--enable-all" when arguments.Length == 1:
                succeeded = DeviceCommands.Execute(arguments);
                return true;

            default:
                return false;
        }
    }

    private static void WriteDiagnostic(string target, SetupApiDeviceProvider provider, object payload)
    {
        File.WriteAllText(
            target,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })
            + "\n\nlastError: " + provider.LastError);

        Current.Shutdown();
    }
}

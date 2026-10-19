using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ControllersOff.Domain;
using ControllersOff.Devices;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ControllersOff.Views;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DeviceCatalog _catalog;
    private readonly ObservableCollection<DeviceGroupView> _groups = [];

    private bool _busy;
    private bool _wasActive;

    public MainWindow(AppSettings settings)
    {
        _settings = settings;
        _catalog = new DeviceCatalog(new SetupApiDeviceProvider(), settings);

        InitializeComponent();
        GroupsControl.ItemsSource = _groups;

        Refresh();
    }

    public void ToggleFlyout()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowFlyout();
    }

    public void ShowFlyout()
    {
        PixelSnapToTray();
        Show();
        Activate();
        SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
    }

    private void PixelSnapToTray()
    {
        // The notification area lives on the primary taskbar, not wherever the cursor happens to be.
        var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        // WorkingArea is in physical pixels while window offsets are in DIPs.
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);

        Left = area.Right / dpi.DpiScaleX - Width - 12;
        Top = Math.Max(area.Top / dpi.DpiScaleY, area.Bottom / dpi.DpiScaleY - Height - 12);
    }

    private void OnActivated(object? sender, EventArgs e) => _wasActive = true;

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // A popup that never got focus must not close itself the moment it opens.
        if (_wasActive && !_busy)
        {
            _wasActive = false;
            Hide();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    public IReadOnlyList<string> RestoreIfNeeded()
    {
        var disabled = _catalog.DisabledByUs();
        if (disabled.Count == 0)
        {
            return [];
        }

        if (!RunElevated(["--toggle-all", string.Join(',', disabled), "on"]))
        {
            return disabled;
        }

        _catalog.Load();
        return [];
    }

    private void Refresh()
    {
        DisabledDeviceLog.Save(_catalog.DisabledByUs());

        _groups.Clear();

        foreach (var group in _catalog.Load())
        {
            _groups.Add(new DeviceGroupView(group.Category, group.Devices.Select(device => new DeviceRow(device))));
        }

        EmptyText.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var off = _catalog.DisabledByUs().Count;
        StatusText.Text = off == 0
            ? Loc.Instance["StatusClean"]
            : Loc.Instance.Format("StatusOff", off);

        var canKeepOnlyKeyboardAndMouse = _catalog.PlanDisableAllExceptKeyboardAndMouse().Count > 0;
        KeepButton.IsHitTestVisible = canKeepOnlyKeyboardAndMouse;

        if (canKeepOnlyKeyboardAndMouse)
        {
            KeepButton.ClearValue(ForegroundProperty);
        }
        else
        {
            KeepButton.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
        }
    }

    private void OnSwitchChecked(object sender, RoutedEventArgs e) => ToggleFromSwitch(RowOf(sender), true);

    private void OnSwitchUnchecked(object sender, RoutedEventArgs e) => ToggleFromSwitch(RowOf(sender), false);

    private void ToggleFromSwitch(DeviceRow row, bool enabled)
    {
        if (!row.RequestToggle(enabled))
        {
            return;
        }

        if (!RunElevated(["--toggle", row.InstanceId, enabled ? "on" : "off"]))
        {
            ShowWarning(Loc.Instance["ToggleFailed"]);
        }
        else
        {
            _catalog.Record(row.InstanceId, enabled);
        }

        ReloadDeviceState(row);
        Refresh();
    }

    private void ReloadDeviceState(DeviceRow row)
    {
        var device = _catalog.Load()
            .SelectMany(group => group.Devices)
            .FirstOrDefault(candidate => candidate.InstanceId == row.InstanceId);

        if (device is not null)
        {
            row.Sync(device);
        }
    }

    private static DeviceRow RowOf(object sender) => (DeviceRow)((FrameworkElement)sender).DataContext;

    private void OnKeepKeyboardAndMouseClick(object sender, RoutedEventArgs e)
    {
        var targets = _catalog.PlanDisableAllExceptKeyboardAndMouse();
        if (targets.Count == 0)
        {
            return;
        }

        if (!RunElevated(["--toggle-all", string.Join(',', targets), "off"]))
        {
            ShowWarning(Loc.Instance["ElevationFailed"]);
        }
        else
        {
            foreach (var target in targets)
            {
                _catalog.Record(target, false);
            }
        }

        Refresh();
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        var disabled = _catalog.DisabledByUs();
        if (disabled.Count == 0)
        {
            ShowWarning(Loc.Instance["NothingToRestore"]);
            return;
        }

        if (!RunElevated(["--toggle-all", string.Join(',', disabled), "on"]))
        {
            ShowWarning(Loc.Instance["RestoreFailed"]);
        }

        Refresh();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _busy = true;

        try
        {
            new SettingsWindow(_settings, _catalog.Load()).ShowDialog();
        }
        finally
        {
            _busy = false;
        }

        App.ApplyTheme(_settings.Theme);
        Refresh();
        Hide();
    }

    private bool RunElevated(string[] arguments)
    {
        _busy = true;

        try
        {
            return App.RunDeviceCommand(arguments);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowWarning(string message)
        => MessageBox.Show(message, Loc.Instance["AppTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}

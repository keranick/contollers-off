using System.Windows;
using System.Windows.Controls;
using ControllersOff.Domain;
using Wpf.Ui.Controls;

namespace ControllersOff.Views;

public partial class SettingsWindow : FluentWindow
{
    private readonly AppSettings _settings;
    private bool _suppressEvents;

    public SettingsWindow(AppSettings settings, IReadOnlyList<DeviceGroup> devices)
    {
        _settings = settings;

        InitializeComponent();

        FillThemeBox();
        FillDeviceBoxes(devices);
        FillLanguageBox();

        _suppressEvents = false;
        AutostartBox.IsChecked = Autostart.IsEnabled();
    }

    private void FillThemeBox()
    {
        ThemeBox.Items.Clear();

        foreach (var (mode, key) in new[]
                 {
                     (ThemeMode.System, "ThemeSystem"),
                     (ThemeMode.Light, "ThemeLight"),
                     (ThemeMode.Dark, "ThemeDark")
                 })
        {
            ThemeBox.Items.Add(new ComboBoxItem { Content = Loc.Instance[key], Tag = mode });
        }

        ThemeBox.SelectedItem = ThemeBox.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, _settings.Theme));
    }

    private void FillDeviceBoxes(IReadOnlyList<DeviceGroup> devices)
    {
        FillDeviceBox(KeyboardBox, devices, DeviceCategory.Keyboard, _settings.DesignatedKeyboardInstanceId);
        FillDeviceBox(MouseBox, devices, DeviceCategory.Mouse, _settings.DesignatedMouseInstanceId);
    }

    private static void FillDeviceBox(System.Windows.Controls.ComboBox box, IReadOnlyList<DeviceGroup> devices, DeviceCategory category, string? designated)
    {
        box.Items.Clear();

        var any = new DeviceChoice { Name = Loc.Instance["AnyDevice"], InstanceId = null };
        box.Items.Add(any);

        foreach (var device in devices.Where(group => group.Category == category).SelectMany(group => group.Devices))
        {
            box.Items.Add(new DeviceChoice { Name = device.FriendlyName, InstanceId = device.InstanceId });
        }

        box.SelectedItem = box.Items.OfType<DeviceChoice>().FirstOrDefault(choice => choice.InstanceId == designated) ?? any;
    }

    private void FillLanguageBox()
    {
        LanguageBox.Items.Clear();

        foreach (var (tag, key) in new[] { ((string?)null, "LanguageSystem"), ("ru", "LanguageRussian"), ("en", "LanguageEnglish") })
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = Loc.Instance[key], Tag = tag });
        }

        LanguageBox.SelectedItem = LanguageBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, _settings.Language))
            ?? LanguageBox.Items.OfType<ComboBoxItem>().First();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressEvents && ThemeBox.SelectedItem is ComboBoxItem { Tag: ThemeMode mode })
        {
            _settings.Theme = mode;
        }
    }

    private void OnDesignatedChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (KeyboardBox.SelectedItem is DeviceChoice keyboard)
        {
            _settings.DesignatedKeyboardInstanceId = keyboard.InstanceId;
        }

        if (MouseBox.SelectedItem is DeviceChoice mouse)
        {
            _settings.DesignatedMouseInstanceId = mouse.InstanceId;
        }
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = AutostartBox.IsChecked == true;

        if (_settings.StartWithWindows)
        {
            Autostart.Enable();
        }
        else
        {
            Autostart.Disable();
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || LanguageBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _settings.Language = item.Tag as string;
        Loc.Instance.UseLanguage(_settings.Language);

        _suppressEvents = true;
        FillThemeBox();
        FillLanguageBox();
        _suppressEvents = false;
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => Close();

    private sealed class DeviceChoice
    {
        public string Name { get; init; } = string.Empty;

        public string? InstanceId { get; init; }
    }
}


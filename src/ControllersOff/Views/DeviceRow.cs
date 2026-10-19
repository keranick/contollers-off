using System.ComponentModel;
using System.Runtime.CompilerServices;
using ControllersOff.Domain;

namespace ControllersOff.Views;

public sealed class DeviceRow : INotifyPropertyChanged
{
    private HidDevice _device;

    public DeviceRow(HidDevice device)
    {
        _device = device;
    }

    public string InstanceId => _device.InstanceId;

    public bool HasName => !string.Equals(_device.FriendlyName, _device.InstanceId, StringComparison.Ordinal);

    public string Name => HasName ? _device.FriendlyName : Loc.Instance["GenericDevice"];

    public string Details => HasName
        ? $"VID {_device.VendorId:X4}  ·  PID {_device.ProductId:X4}"
        : _device.InstanceId;

    public bool IsEnabled => _device.IsEnabled;

    public bool RequestToggle(bool enabled)
    {
        return enabled != _device.IsEnabled;
    }

    public void Sync(HidDevice device)
    {
        _device = device;
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Details));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class DeviceGroupView
{
    public DeviceGroupView(DeviceCategory category, IEnumerable<DeviceRow> devices)
    {
        Category = category;
        Devices = [.. devices];
    }

    public DeviceCategory Category { get; }

    public string Title => Loc.Instance[$"Category{Category}"];

    public IReadOnlyList<DeviceRow> Devices { get; }
}
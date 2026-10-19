using ControllersOff.Domain;

namespace ControllersOff.Tests;

internal sealed class FakeDeviceProvider(params HidDevice[] devices) : IDeviceProvider
{
    private readonly List<HidDevice> _devices = [..devices];

    public bool WritesFail { get; set; }

    public bool HideDisabled { get; init; } = true;

    public IReadOnlyList<HidDevice> Enumerate() => _devices.Where(device => device.IsEnabled || !HideDisabled).ToArray();

    public bool SetEnabled(string instanceId, bool enabled)
    {
        if (WritesFail)
        {
            return false;
        }

        var index = _devices.FindIndex(device => device.InstanceId == instanceId);
        if (index < 0)
        {
            return false;
        }

        _devices[index] = _devices[index] with { IsEnabled = enabled };
        return true;
    }

    public HidDevice Get(string instanceId) => _devices.Single(device => device.InstanceId == instanceId);
}

internal static class Devices
{
    public static HidDevice Create(string instanceId, DeviceCategory category, bool enabled = true) => new(
        instanceId,
        $"{category} device",
        VendorId: 0x0EB7,
        ProductId: 0x0001,
        UsagePage: UsagePageOf(category),
        Usage: UsageOf(category),
        IsEnabled: enabled);

    private static ushort UsagePageOf(DeviceCategory category) => category switch
    {
        DeviceCategory.Keyboard => 0x01,
        DeviceCategory.Mouse => 0x01,
        DeviceCategory.Gamepad => 0x01,
        DeviceCategory.Wheel => 0x01,
        DeviceCategory.Custom => 0xFF,
        _ => 0x0B
    };

    private static ushort UsageOf(DeviceCategory category) => category switch
    {
        DeviceCategory.Keyboard => 0x06,
        DeviceCategory.Mouse => 0x02,
        DeviceCategory.Gamepad => 0x05,
        DeviceCategory.Wheel => 0x08,
        DeviceCategory.Custom => 0x01,
        _ => 0x01
    };
}
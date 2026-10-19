namespace ControllersOff.Domain;

public sealed record HidDevice(
    string InstanceId,
    string FriendlyName,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    bool IsEnabled)
{
    public DeviceCategory Category => HidUsage.Classify(UsagePage, Usage);
}

public sealed record DeviceGroup(DeviceCategory Category, IReadOnlyList<HidDevice> Devices);
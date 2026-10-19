namespace ControllersOff.Domain;

public interface IDeviceProvider
{
    IReadOnlyList<HidDevice> Enumerate();

    bool SetEnabled(string instanceId, bool enabled);
}
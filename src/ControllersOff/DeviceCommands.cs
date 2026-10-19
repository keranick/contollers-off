using ControllersOff.Devices;
using ControllersOff.Domain;

namespace ControllersOff;

public static class DeviceCommands
{
    public static bool Execute(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return false;
        }

        return arguments.FirstOrDefault() switch
        {
            "--toggle" when arguments.Count == 3 => Toggle(arguments[1], arguments[2] == "on"),
            "--toggle-all" when arguments.Count == 3 => ToggleAll(arguments[1], arguments[2] == "on"),
            "--enable-all" when arguments.Count == 1 => EnableEveryDevice(),
            _ => false
        };
    }

    private static bool EnableEveryDevice() => new SetupApiDeviceProvider().EnableAllDisabled();

    private static bool Toggle(string instanceId, bool enabled)
    {
        var provider = new SetupApiDeviceProvider();
        var result = provider.SetEnabled(instanceId, enabled);
        Trace.Write($"toggle '{instanceId}' -> {enabled} = {result} error={provider.LastError ?? "none"}");

        return result;
    }

    private static bool ToggleAll(string instanceIds, bool enabled)
    {
        var provider = new SetupApiDeviceProvider();
        var ids = instanceIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var success = true;

        foreach (var instanceId in ids)
        {
            success &= provider.SetEnabled(instanceId, enabled);
        }

        Trace.Write($"toggle-all count={ids.Length} -> {enabled} = {success} error={provider.LastError ?? "none"}");

        return success;
    }
}

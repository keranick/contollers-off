namespace ControllersOff.Domain;

public sealed class DeviceCatalog
{
    private readonly IDeviceProvider _provider;
    private readonly AppSettings _settings;
    private readonly Dictionary<string, HidDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HidDevice> _disabledByUs = new(StringComparer.OrdinalIgnoreCase);

    public DeviceCatalog(IDeviceProvider provider, AppSettings settings)
    {
        _provider = provider;
        _settings = settings;
    }

    public bool HasDevicesDisabledByUs => _disabledByUs.Count > 0;

    public IReadOnlyList<DeviceGroup> Load()
    {
        foreach (var device in _provider.Enumerate())
        {
            _devices[device.InstanceId] = device;
        }

        foreach (var instanceId in _disabledByUs.Keys.ToArray().Where(id => _devices.TryGetValue(id, out var d) && d.IsEnabled))
        {
            _disabledByUs.Remove(instanceId);
        }

        // Disabled devices drop out of the enumeration, so the snapshot has to keep them.
        foreach (var (instanceId, device) in _disabledByUs)
        {
            if (!_devices.ContainsKey(instanceId))
            {
                _devices[instanceId] = device with { IsEnabled = false };
            }
        }

        return _devices.Values
            .Where(device => device.Category != DeviceCategory.Unknown)
            .GroupBy(device => device.Category)
            .OrderBy(group => group.Key)
            .Select(group => new DeviceGroup(group.Key, group.OrderBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase).ToArray()))
            .ToArray();
    }

    public bool SetEnabled(string instanceId, bool enabled)
    {
        if (!_devices.ContainsKey(instanceId))
        {
            Load();
        }

        if (!_devices.TryGetValue(instanceId, out var device))
        {
            throw new KeyNotFoundException(instanceId);
        }

        if (device.IsEnabled == enabled || !_provider.SetEnabled(instanceId, enabled))
        {
            return device.IsEnabled == enabled;
        }

        Record(instanceId, enabled);
        return true;
    }

    public void Record(string instanceId, bool enabled)
    {
        if (!_devices.TryGetValue(instanceId, out var device))
        {
            return;
        }

        _devices[instanceId] = device with { IsEnabled = enabled };

        if (enabled)
        {
            _disabledByUs.Remove(instanceId);
        }
        else
        {
            _disabledByUs[instanceId] = device;
        }
    }

    public IReadOnlyList<string> PlanDisableAllExceptKeyboardAndMouse()
    {
        return Load()
            .SelectMany(group => group.Devices)
            .Where(device => device.IsEnabled && device.Category != DeviceCategory.Unknown && !IsProtected(device))
            .Select(device => device.InstanceId)
            .ToArray();
    }

    public IReadOnlyList<string> DisabledByUs() => _disabledByUs.Keys.ToArray();

    public IReadOnlyList<string> DisableAllExceptKeyboardAndMouse()
    {
        var disabled = new List<string>();

        foreach (var instanceId in PlanDisableAllExceptKeyboardAndMouse())
        {
            if (SetEnabled(instanceId, false))
            {
                disabled.Add(instanceId);
            }
        }

        return disabled;
    }

    public IReadOnlyList<string> RestoreInitialState()
    {
        var restored = new List<string>();

        foreach (var instanceId in _disabledByUs.Keys.ToArray())
        {
            if (_provider.SetEnabled(instanceId, true))
            {
                restored.Add(instanceId);
                _disabledByUs.Remove(instanceId);
                _devices.Remove(instanceId);
            }
        }

        return restored;
    }

    private bool IsProtected(HidDevice device) => device.Category switch
    {
        DeviceCategory.Keyboard => IsDesignated(device, _settings.DesignatedKeyboardInstanceId),
        DeviceCategory.Mouse => IsDesignated(device, _settings.DesignatedMouseInstanceId),
        _ => false
    };

    private static bool IsDesignated(HidDevice device, string? designatedInstanceId)
        => designatedInstanceId is null || Matches(device.InstanceId, designatedInstanceId);

    private static bool Matches(string instanceId, string other)
        => string.Equals(instanceId, other, StringComparison.OrdinalIgnoreCase);
}
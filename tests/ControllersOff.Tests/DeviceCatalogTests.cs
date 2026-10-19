using ControllersOff.Domain;

namespace ControllersOff.Tests;

public class DeviceCatalogTests
{
    [Fact]
    public void Load_HidesDevicesWithoutKnownUsage()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("kbd", DeviceCategory.Keyboard),
            Devices.Create("unknown", DeviceCategory.Unknown));

        var groups = new DeviceCatalog(provider, new AppSettings()).Load();

        Assert.Equal(["kbd"], groups.SelectMany(g => g.Devices).Select(d => d.InstanceId));
    }

    [Fact]
    public void Load_GroupsDevicesByCategory()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("kbd", DeviceCategory.Keyboard),
            Devices.Create("wheel", DeviceCategory.Wheel),
            Devices.Create("shifter", DeviceCategory.Custom),
            Devices.Create("pad", DeviceCategory.Gamepad));

        var groups = new DeviceCatalog(provider, new AppSettings()).Load();

        Assert.Equal(
            [DeviceCategory.Keyboard, DeviceCategory.Gamepad, DeviceCategory.Wheel, DeviceCategory.Custom],
            groups.Select(g => g.Category));
    }

    [Fact]
    public void SetEnabled_DisablesDeviceAndTracksIt()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        Assert.True(catalog.SetEnabled("pad", enabled: false));
        Assert.False(provider.Get("pad").IsEnabled);
        Assert.Equal(["pad"], catalog.RestoreInitialState());
        Assert.True(provider.Get("pad").IsEnabled);
    }

    [Fact]
    public void Load_KeepsDevicesWeDisabledEvenThoughEnumerationLosesThem()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("pad", DeviceCategory.Gamepad),
            Devices.Create("wheel", DeviceCategory.Wheel));
        var catalog = new DeviceCatalog(provider, new AppSettings());
        catalog.Load();

        catalog.SetEnabled("pad", enabled: false);
        catalog.SetEnabled("wheel", enabled: false);

        var listed = catalog.Load().SelectMany(g => g.Devices).ToDictionary(d => d.InstanceId, d => d.IsEnabled);

        Assert.Equal([false, false], [listed["pad"], listed["wheel"]]);
    }

    [Fact]
    public void SetEnabled_ReEnablesDeviceThatVanishedFromEnumeration()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad, enabled: false));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        Assert.Empty(catalog.Load());
        Assert.Throws<KeyNotFoundException>(() => catalog.SetEnabled("pad", enabled: true));
    }

    [Fact]
    public void RestoreInitialState_ForgetsRestoredDevices()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad));
        var catalog = new DeviceCatalog(provider, new AppSettings());
        catalog.Load();
        catalog.SetEnabled("pad", enabled: false);

        Assert.True(catalog.HasDevicesDisabledByUs);
        Assert.Equal(["pad"], catalog.RestoreInitialState());
        Assert.False(catalog.HasDevicesDisabledByUs);
        Assert.Empty(catalog.Load().SelectMany(g => g.Devices).Where(d => !d.IsEnabled));
    }

    [Fact]
    public void RestoreInitialState_LeavesDevicesDisabledBeforeStartupAlone()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("pad", DeviceCategory.Gamepad, enabled: false),
            Devices.Create("wheel", DeviceCategory.Wheel));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        catalog.SetEnabled("wheel", enabled: false);
        var restored = catalog.RestoreInitialState();

        Assert.Equal(["wheel"], restored);
        Assert.False(provider.Get("pad").IsEnabled);
        Assert.True(provider.Get("wheel").IsEnabled);
    }

    [Fact]
    public void RestoreInitialState_SkipsDevicesWeReEnabledOurselves()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        catalog.SetEnabled("pad", enabled: false);
        catalog.SetEnabled("pad", enabled: true);

        Assert.Empty(catalog.RestoreInitialState());
    }

    [Fact]
    public void RestoreInitialState_KeepsFailedReenableTracked()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad));
        var catalog = new DeviceCatalog(provider, new AppSettings());
        catalog.SetEnabled("pad", enabled: false);

        provider.WritesFail = true;
        Assert.Empty(catalog.RestoreInitialState());
        provider.WritesFail = false;

        Assert.Equal(["pad"], catalog.RestoreInitialState());
    }

    [Fact]
    public void SetEnabled_ReturnsFalseAndChangesNothingWhenDeviceWriteFails()
    {
        var provider = new FakeDeviceProvider(Devices.Create("pad", DeviceCategory.Gamepad)) { WritesFail = true };
        var catalog = new DeviceCatalog(provider, new AppSettings());

        Assert.False(catalog.SetEnabled("pad", enabled: false));
        Assert.Empty(catalog.RestoreInitialState());
    }

    [Fact]
    public void SetEnabled_ThrowsWhenDeviceIsGone()
    {
        var catalog = new DeviceCatalog(new FakeDeviceProvider(), new AppSettings());

        Assert.Throws<KeyNotFoundException>(() => catalog.SetEnabled("missing", enabled: false));
    }

    [Fact]
    public void DisableAllExceptKeyboardAndMouse_KeepsEveryKeyboardAndMouseByDefault()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("kbd-1", DeviceCategory.Keyboard),
            Devices.Create("kbd-2", DeviceCategory.Keyboard),
            Devices.Create("mouse-1", DeviceCategory.Mouse),
            Devices.Create("pad", DeviceCategory.Gamepad),
            Devices.Create("wheel", DeviceCategory.Wheel),
            Devices.Create("shifter", DeviceCategory.Custom),
            Devices.Create("unknown", DeviceCategory.Unknown));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        var disabled = catalog.DisableAllExceptKeyboardAndMouse();

        Assert.Equal(["pad", "shifter", "wheel"], disabled.Order().ToArray());
        Assert.True(provider.Get("kbd-1").IsEnabled);
        Assert.True(provider.Get("mouse-1").IsEnabled);
    }

    [Fact]
    public void DisableAllExceptKeyboardAndMouse_DisablesNonDesignatedKeyboardAndMouse()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("kbd-main", DeviceCategory.Keyboard),
            Devices.Create("kbd-usb", DeviceCategory.Keyboard),
            Devices.Create("mouse-main", DeviceCategory.Mouse),
            Devices.Create("mouse-usb", DeviceCategory.Mouse));
        var settings = new AppSettings
        {
            DesignatedKeyboardInstanceId = "kbd-main",
            DesignatedMouseInstanceId = "mouse-main"
        };

        var disabled = new DeviceCatalog(provider, settings).DisableAllExceptKeyboardAndMouse();

        Assert.Equal(["kbd-usb", "mouse-usb"], disabled.Order().ToArray());
    }

    [Fact]
    public void DisableAllExceptKeyboardAndMouse_SkipsAlreadyDisabledDevices()
    {
        var provider = new FakeDeviceProvider(
            Devices.Create("pad", DeviceCategory.Gamepad, enabled: false),
            Devices.Create("wheel", DeviceCategory.Wheel));
        var catalog = new DeviceCatalog(provider, new AppSettings());

        Assert.Equal(["wheel"], catalog.DisableAllExceptKeyboardAndMouse());
        Assert.Equal(["wheel"], catalog.RestoreInitialState());
        Assert.False(provider.Get("pad").IsEnabled);
        Assert.True(provider.Get("wheel").IsEnabled);
    }
}
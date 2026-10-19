namespace ControllersOff.Domain;

public enum DeviceCategory
{
    Unknown,
    Keyboard,
    Mouse,
    Gamepad,
    Wheel,
    Custom
}

public static class HidUsage
{
    private const ushort GenericDesktop = 0x01;
    private const ushort Simulation = 0x02;
    private const ushort KeyboardPageLegacy = 0x03;
    private const ushort KeyboardPage = 0x07;
    private const ushort VendorDefined = 0xFF;

    public static DeviceCategory Classify(ushort usagePage, ushort usage) => usagePage switch
    {
        GenericDesktop => usage switch
        {
            0x02 => DeviceCategory.Mouse,
            0x04 or 0x05 => DeviceCategory.Gamepad,
            0x06 => DeviceCategory.Keyboard,
            0x08 => DeviceCategory.Wheel,
            _ => DeviceCategory.Unknown
        },
        Simulation => DeviceCategory.Wheel,
        KeyboardPageLegacy or KeyboardPage => DeviceCategory.Keyboard,
        // Wheel bases and shifters (Fanatec, Thrustmaster, Logitech) report vendor-defined usages instead of a standard page.
        >= VendorDefined => DeviceCategory.Custom,
        _ => DeviceCategory.Unknown
    };
}
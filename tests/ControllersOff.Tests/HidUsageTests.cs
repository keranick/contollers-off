using ControllersOff.Domain;

namespace ControllersOff.Tests;

public class HidUsageTests
{
    [Theory]
    [InlineData(0x01, 0x06, DeviceCategory.Keyboard)]
    [InlineData(0x01, 0x02, DeviceCategory.Mouse)]
    [InlineData(0x01, 0x04, DeviceCategory.Gamepad)]
    [InlineData(0x01, 0x05, DeviceCategory.Gamepad)]
    [InlineData(0x01, 0x08, DeviceCategory.Wheel)]
    [InlineData(0x02, 0x00, DeviceCategory.Wheel)]
    [InlineData(0x03, 0x04, DeviceCategory.Keyboard)]
    [InlineData(0x07, 0x06, DeviceCategory.Keyboard)]
    public void ClassifiesKnownUsages(ushort usagePage, ushort usage, DeviceCategory expected)
        => Assert.Equal(expected, HidUsage.Classify(usagePage, usage));

    [Theory]
    [InlineData(0xFF, 0x01)]
    [InlineData(0xFF00, 0x01)]
    [InlineData(0xFF01, 0x02)]
    public void VendorDefinedCountsAsCustomSoShiftersStayVisible(ushort usagePage, ushort usage)
        => Assert.Equal(DeviceCategory.Custom, HidUsage.Classify(usagePage, usage));

    [Theory]
    [InlineData(0x01, 0x01)]
    [InlineData(0x0B, 0x01)]
    [InlineData(0x04, 0x02)]
    public void ClassifiesUnknownUsagesAsUnknown(ushort usagePage, ushort usage)
        => Assert.Equal(DeviceCategory.Unknown, HidUsage.Classify(usagePage, usage));
}
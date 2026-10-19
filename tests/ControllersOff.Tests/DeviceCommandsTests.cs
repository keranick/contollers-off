using ControllersOff;

namespace ControllersOff.Tests;

public class DeviceCommandsTests
{
    [Theory]
    [InlineData("--toggle")]
    [InlineData("--toggle-all")]
    [InlineData("--nonsense")]
    [InlineData("--nonsense", "a", "b", "c")]
    public void MalformedArgumentsAreRejectedWithoutThrowing(params string[] arguments)
        => Assert.False(DeviceCommands.Execute(arguments));

    [Fact]
    public void EmptyArgumentsAreRejected()
        => Assert.False(DeviceCommands.Execute([]));
}
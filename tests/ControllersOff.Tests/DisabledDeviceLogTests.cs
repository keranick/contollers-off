using System.IO;
using ControllersOff.Domain;

namespace ControllersOff.Tests;

public class DisabledDeviceLogTests
{
    [Fact]
    public void MissingFileMeansNothingIsPending()
        => Assert.Empty(DisabledDeviceLog.Load(TempPath()));

    [Fact]
    public void SaveWithNoDevicesClearsThePendingList()
    {
        var file = TempPath();

        DisabledDeviceLog.Save(["pad"], file);
        DisabledDeviceLog.Save([], file);

        Assert.Empty(DisabledDeviceLog.Load(file));
    }

    [Fact]
    public void SavePersistsDevicesThatStillNeedRestoring()
    {
        var file = TempPath();

        DisabledDeviceLog.Save(["pad", "wheel"], file);

        Assert.Equal(["pad", "wheel"], DisabledDeviceLog.Load(file));
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
}
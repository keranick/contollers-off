using System.IO;
using ControllersOff.Domain;

namespace ControllersOff.Tests;

public class AutostartTests
{
    [Theory]
    [InlineData(@"C:\Tools\ControllersOff.exe")]
    [InlineData(@"C:\Program Files\Controllers Off\ControllersOff.exe")]
    public void BuildCommandLine_QuotesPathAndStartsInTray(string path)
        => Assert.Equal($"\"{path}\" --tray", Autostart.BuildCommandLine(path));
}

public class SettingsStoreTests
{
    [Fact]
    public void Load_MissingFileYieldsDefaults()
    {
        var settings = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));

        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.False(settings.StartWithWindows);
        Assert.Null(settings.DesignatedKeyboardInstanceId);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsEverySetting()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        var expected = new AppSettings
        {
            Theme = ThemeMode.Dark,
            StartWithWindows = true,
            DesignatedKeyboardInstanceId = "kbd-main",
            DesignatedMouseInstanceId = "mouse-main",
            Language = "en-US"
        };

        SettingsStore.Save(expected, file);
        var actual = SettingsStore.Load(file);

        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.DesignatedKeyboardInstanceId, actual.DesignatedKeyboardInstanceId);
        Assert.Equal(expected.DesignatedMouseInstanceId, actual.DesignatedMouseInstanceId);
        Assert.Equal(expected.Language, actual.Language);
    }

    [Fact]
    public void Load_CorruptedFileYieldsDefaults()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(file, "{ not json");

        Assert.Equal(ThemeMode.System, SettingsStore.Load(file).Theme);
    }
}
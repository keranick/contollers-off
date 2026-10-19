using System.IO;
using System.Text.Json;

namespace ControllersOff.Domain;

public static class DisabledDeviceLog
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControllersOff",
        "pending-restore.json");

    public static string[] Load(string? path = null)
    {
        var file = path ?? FilePath;

        return File.Exists(file)
            ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(file)) ?? []
            : [];
    }

    public static void Save(IEnumerable<string> instanceIds, string? path = null)
    {
        var file = path ?? FilePath;
        var ids = instanceIds.ToArray();

        if (ids.Length == 0)
        {
            File.Delete(file);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(ids));
    }
}
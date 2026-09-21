using System.Text.Json;

namespace RoweMod.App;

sealed class LocalSettings
{
    public string? GamePaks { get; set; }
    public string? UnrealEditor { get; set; }

    static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoweMod", "settings.json");

    public static LocalSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new LocalSettings();
            return JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(FilePath)) ?? new LocalSettings();
        }
        catch
        {
            return new LocalSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

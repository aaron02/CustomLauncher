using System.Text.Json;
using AscEmu.Launcher.Models;

namespace AscEmu.Launcher.Services;

public static class SettingsService
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AscEmu", "Launcher", "launcher.json");

    public static LauncherSettings Load()
    {
        try { return File.Exists(SettingsPath) ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new() : new(); }
        catch { return new(); }
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}

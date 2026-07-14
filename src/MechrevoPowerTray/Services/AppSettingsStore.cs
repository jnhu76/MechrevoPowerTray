using System.Text.Json;
using MechrevoPowerTray.Models;

namespace MechrevoPowerTray.Services;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    internal AppSettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MechrevoPowerTray");

        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    internal AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? new AppSettings();

            if (settings.LastSuccessfulMode is { } mode && !mode.IsWhitelisted())
            {
                settings.LastSuccessfulMode = null;
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    internal void Save(AppSettings settings)
    {
        var tempPath = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }
}

internal sealed class AppSettings
{
    public bool SyncWindowsPowerPlan { get; set; } = true;

    public bool RestoreLastModeAtStartup { get; set; }

    public OemPowerMode? LastSuccessfulMode { get; set; }
}

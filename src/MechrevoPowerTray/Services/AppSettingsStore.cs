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
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MechrevoPowerTray"))
    {
    }

    internal AppSettingsStore(string directory)
    {
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

            if (settings.LastOemRequestAcceptedMode is null)
            {
                settings.LastOemRequestAcceptedMode = MigrateLastSuccessfulMode(json);
            }

            if (settings.LastOemRequestAcceptedMode is { } mode && !mode.IsWhitelisted())
            {
                settings.LastOemRequestAcceptedMode = null;
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

    private static OemPowerMode? MigrateLastSuccessfulMode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("LastSuccessfulMode", out var legacyProp))
            {
                return null;
            }

            if (legacyProp.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var raw = legacyProp.GetByte();
            var mode = (OemPowerMode)raw;

            return mode.IsWhitelisted() ? mode : null;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class AppSettings
{
    public bool SyncWindowsPowerPlan { get; set; } = true;

    public bool RestoreLastModeAtStartup { get; set; }

    public OemPowerMode? LastOemRequestAcceptedMode { get; set; }
}

using MechrevoPowerTray.Models;
using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _testDir;

    public AppSettingsStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MPT_SettingsTest_" + Guid.NewGuid());
    }

    [Fact]
    public void LegacyLastSuccessfulMode_LoadsIntoLastOemRequestAcceptedMode()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), """{"LastSuccessfulMode": 2}""");
        var store = new AppSettingsStore(_testDir);
        var settings = store.Load();
        Assert.Equal(OemPowerMode.Balanced, settings.LastOemRequestAcceptedMode);
    }

    [Fact]
    public void LegacyLastSuccessfulMode0_IgnoresAndLeavesNull()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), """{"LastSuccessfulMode": 0}""");
        var store = new AppSettingsStore(_testDir);
        var settings = store.Load();
        Assert.Null(settings.LastOemRequestAcceptedMode);
    }

    [Fact]
    public void LegacyLastSuccessfulMode4_IgnoresAndLeavesNull()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), """{"LastSuccessfulMode": 4}""");
        var store = new AppSettingsStore(_testDir);
        var settings = store.Load();
        Assert.Null(settings.LastOemRequestAcceptedMode);
    }

    [Fact]
    public void NewLastOemRequestAcceptedMode_TakesPriority()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), """{"LastOemRequestAcceptedMode": 3, "LastSuccessfulMode": 2}""");
        var store = new AppSettingsStore(_testDir);
        var settings = store.Load();
        Assert.Equal(OemPowerMode.Performance, settings.LastOemRequestAcceptedMode);
    }

    [Fact]
    public void Save_WritesOnlyLastOemRequestAcceptedMode()
    {
        var store = new AppSettingsStore(_testDir);
        var settings = new AppSettings
        {
            LastOemRequestAcceptedMode = OemPowerMode.Quiet,
            SyncWindowsPowerPlan = true,
            RestoreLastModeAtStartup = false
        };
        store.Save(settings);
        var written = File.ReadAllText(Path.Combine(_testDir, "settings.json"));
        Assert.Contains("LastOemRequestAcceptedMode", written);
        Assert.DoesNotContain("LastSuccessfulMode", written);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), "not json {{{");
        var store = new AppSettingsStore(_testDir);
        var settings = store.Load();
        Assert.NotNull(settings);
        Assert.Null(settings.LastOemRequestAcceptedMode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }
}

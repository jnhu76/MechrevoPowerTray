namespace MechrevoPowerTray.Models;

internal enum OemPowerMode : byte
{
    Quiet = 1,
    Balanced = 2,
    Performance = 3
}

internal static class OemPowerModeExtensions
{
    internal static string DisplayName(this OemPowerMode mode) => mode switch
    {
        OemPowerMode.Quiet => "安静",
        OemPowerMode.Balanced => "均衡",
        OemPowerMode.Performance => "性能",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知 OEM 性能模式。")
    };

    internal static Guid WindowsPowerScheme(this OemPowerMode mode) => mode switch
    {
        OemPowerMode.Quiet =>
            new Guid("a1841308-3541-4fab-bc81-f71556f20b4a"), // Power saver
        OemPowerMode.Balanced =>
            new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"), // Balanced
        OemPowerMode.Performance =>
            new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"), // High performance
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知 OEM 性能模式。")
    };

    internal static bool IsWhitelisted(this OemPowerMode mode) =>
        mode is OemPowerMode.Quiet or OemPowerMode.Balanced or OemPowerMode.Performance;
}

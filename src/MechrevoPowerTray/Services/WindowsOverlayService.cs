using System.Runtime.InteropServices;
using MechrevoPowerTray.Models;

namespace MechrevoPowerTray.Services;

internal sealed class WindowsOverlayService
{
    private static readonly Guid BetterBatteryOverlay =
        new("961cc777-2547-4f9d-8174-7d86181b8a7a");

    private static readonly Guid DefaultOverlay =
        new("00000000-0000-0000-0000-000000000000");

    private static readonly Guid BestPerformanceOverlay =
        new("ded574b5-45a0-4f42-8737-46345c09c238");

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveOverlayScheme(Guid overlaySchemeGuid);

    internal PowerPlanSwitchResult SetForMode(OemPowerMode mode)
    {
        if (!mode.IsWhitelisted())
        {
            return new PowerPlanSwitchResult(
                false,
                $"拒绝为未知模式设置 Windows 电源叠加方案：{(byte)mode}。");
        }

        var overlayGuid = mode switch
        {
            OemPowerMode.Quiet => BetterBatteryOverlay,
            OemPowerMode.Balanced => DefaultOverlay,
            OemPowerMode.Performance => BestPerformanceOverlay,
            _ => DefaultOverlay
        };

        var result = PowerSetActiveOverlayScheme(overlayGuid);

        if (result == 0)
        {
            return new PowerPlanSwitchResult(
                true,
                $"Windows 电源叠加方案已同步为“{mode.DisplayName()}”对应方案。");
        }

        return new PowerPlanSwitchResult(
            false,
            $"Windows 电源叠加方案切换失败：错误码 {result}",
            result);
    }
}

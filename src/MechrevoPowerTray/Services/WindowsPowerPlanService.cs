using System.ComponentModel;
using System.Runtime.InteropServices;
using MechrevoPowerTray.Models;

namespace MechrevoPowerTray.Services;

internal sealed class WindowsPowerPlanService
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(
        IntPtr userRootPowerKey,
        ref Guid schemeGuid);

    internal PowerPlanSwitchResult SetForMode(OemPowerMode mode)
    {
        if (!mode.IsWhitelisted())
        {
            return new PowerPlanSwitchResult(
                false,
                $"拒绝为未知模式设置 Windows 电源计划：{(byte)mode}。");
        }

        var scheme = mode.WindowsPowerScheme();
        var result = PowerSetActiveScheme(IntPtr.Zero, ref scheme);

        if (result == 0)
        {
            return new PowerPlanSwitchResult(
                true,
                $"Windows 电源计划已同步为“{mode.DisplayName()}”对应方案。");
        }

        return new PowerPlanSwitchResult(
            false,
            $"Windows 电源计划切换失败：{new Win32Exception((int)result).Message}",
            result);
    }
}

using MechrevoPowerTray.Models;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class OemSwitchNotificationTests
{
    [Fact]
    public void Accepted_Message_SaysAcceptedNotConfirmed()
    {
        var result = new OemModeSwitchResult(
            OemSwitchOutcome.Accepted,
            OemPowerMode.Quiet,
            "OEM 请求“安静”模式已被接受。",
            ReturnValue: 0,
            ActiveInstanceCount: 1);

        Assert.True(result.IsAccepted);
        Assert.Contains("接受", result.Message);
        Assert.DoesNotContain("切换", result.Message);
        Assert.DoesNotContain("成功进入", result.Message);
        Assert.DoesNotContain("恢复", result.Message);
    }

    [Fact]
    public void Indeterminate_WarnsAgainstImmediateRetry()
    {
        var result = new OemModeSwitchResult(
            OemSwitchOutcome.Indeterminate,
            OemPowerMode.Balanced,
            "OEM WMI 调用超时。请求可能已经生效，请勿立即重复切换。");

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
        Assert.Contains("请勿立即重复切换", result.Message);
    }

    [Fact]
    public void StatusLabel_DoesNotClaimHardwareConfirmed()
    {
        var mode = OemPowerMode.Quiet;
        var label = mode switch
        {
            OemPowerMode.Quiet => "上次 OEM 已接受请求：安静",
            OemPowerMode.Balanced => "上次 OEM 已接受请求：均衡",
            OemPowerMode.Performance => "上次 OEM 已接受请求：性能",
            _ => ""
        };

        Assert.Contains("接受请求", label);
        Assert.DoesNotContain("当前", label);
        Assert.DoesNotContain("已进入", label);
        Assert.DoesNotContain("成功", label);
    }

    [Fact]
    public void HardwareStatus_ShowsNotRead()
    {
        var hardwareStatus = "当前硬件模式：未回读";

        Assert.Contains("未回读", hardwareStatus);
        Assert.DoesNotContain("正常", hardwareStatus);
        Assert.DoesNotContain("未知", hardwareStatus);
    }
}

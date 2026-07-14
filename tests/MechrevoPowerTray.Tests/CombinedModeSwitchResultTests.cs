using MechrevoPowerTray.Models;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class CombinedModeSwitchResultTests
{
    private static readonly OemModeSwitchResult AcceptedResult = new(
        OemSwitchOutcome.Accepted, OemPowerMode.Balanced, "OEM 已接受", 0, 1);

    private static readonly OemModeSwitchResult RejectedResult = new(
        OemSwitchOutcome.Rejected, OemPowerMode.Balanced, "OEM 被拒绝", ReturnValue: 5);

    private static readonly PowerPlanSwitchResult PlanSuccess = new(true, "成功");
    private static readonly PowerPlanSwitchResult PlanFail = new(false, "失败", 1);

    [Fact]
    public void AllSuccessful_WhenAllThreeSucceed()
    {
        var combined = new CombinedModeSwitchResult(
            OemPowerMode.Balanced, AcceptedResult, PlanSuccess, PlanSuccess);

        Assert.True(combined.AllSuccessful);
        Assert.True(combined.OemAccepted);
    }

    [Fact]
    public void NotAllSuccessful_WhenOemFails()
    {
        var combined = new CombinedModeSwitchResult(
            OemPowerMode.Balanced, RejectedResult, PlanSuccess, PlanSuccess);

        Assert.False(combined.AllSuccessful);
        Assert.False(combined.OemAccepted);
    }

    [Fact]
    public void CombinedMessage_IncludesPlanFailure()
    {
        var combined = new CombinedModeSwitchResult(
            OemPowerMode.Balanced, AcceptedResult, PlanFail, PlanSuccess);

        Assert.Contains("OEM 已接受", combined.CombinedMessage);
        Assert.Contains("电源计划", combined.CombinedMessage);
        Assert.DoesNotContain("叠加方案", combined.CombinedMessage);
    }

    [Fact]
    public void CombinedMessage_IncludesAllFailures()
    {
        var combined = new CombinedModeSwitchResult(
            OemPowerMode.Balanced, AcceptedResult, PlanFail, PlanFail);

        Assert.Contains("电源计划", combined.CombinedMessage);
        Assert.Contains("叠加方案", combined.CombinedMessage);
    }
}

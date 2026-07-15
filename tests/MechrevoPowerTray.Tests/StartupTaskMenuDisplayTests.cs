using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class StartupTaskMenuDisplayTests
{
    [Fact]
    public void MissingTask_ShowsNotEnabledText_WithEnableAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.Missing);

        Assert.Contains("未启用", stateText);
        Assert.Equal("启用自动启动", actionText);
    }

    [Fact]
    public void Present_ShowsEnabledText_WithDisableAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.Present);

        Assert.Contains("已启用", stateText);
        Assert.Equal("禁用自动启动", actionText);
    }

    [Fact]
    public void Invalid_ShowsErrorText_NoAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.Invalid);

        Assert.Contains("异常", stateText);
        Assert.Null(actionText);
    }
}

using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class StartupTaskMenuDisplayTests
{
    [Fact]
    public void MissingTask_ShowsPausedText_NoAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.Missing);

        Assert.Contains("已暂停", stateText);
        Assert.Null(actionText);
    }

    [Fact]
    public void LegacyPresent_ShowsDetectedText_WithRemoveAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.LegacyPresent);

        Assert.Contains("旧版", stateText);
        Assert.Equal("删除旧版启动任务", actionText);
    }

    [Fact]
    public void Invalid_ShowsErrorText_NoAction()
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(StartupTaskState.Invalid);

        Assert.Contains("异常", stateText);
        Assert.Null(actionText);
    }
}

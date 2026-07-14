namespace MechrevoPowerTray.Services;

internal static class StartupTaskMenuDisplay
{
    internal static (string StateText, string? ActionText) GetDisplay(StartupTaskState state) => state switch
    {
        StartupTaskState.Missing => ("登录自动启动：未启用", "启用自动启动"),
        StartupTaskState.Present => ("登录自动启动：已启用", "禁用自动启动"),
        StartupTaskState.Invalid => ("启动任务配置异常", null),
        _ => ("启动任务状态未知", null)
    };
}

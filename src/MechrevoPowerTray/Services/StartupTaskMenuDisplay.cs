namespace MechrevoPowerTray.Services;

internal static class StartupTaskMenuDisplay
{
    internal static (string StateText, string? ActionText) GetDisplay(StartupTaskState state) => state switch
    {
        StartupTaskState.Missing => ("登录自动启动：v0.0.2 已暂停", null),
        StartupTaskState.LegacyPresent => ("检测到旧版高权限启动任务", "删除旧版启动任务"),
        StartupTaskState.Invalid => ("启动任务配置异常", null),
        _ => ("启动任务状态未知", null)
    };
}

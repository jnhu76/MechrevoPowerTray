namespace MechrevoPowerTray.Services;

internal enum StartupTaskState
{
    Missing,
    Present,
    Invalid
}

internal sealed class StartupTaskService
{
    private const string TaskName = "MechrevoPowerTray";
    private const string SchtasksExe = "schtasks.exe";
    private static readonly TimeSpan SchtasksTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessRunner _processRunner;

    internal StartupTaskService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    internal async Task<StartupTaskState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                SchtasksFullPath,
                ["/Query", "/TN", TaskName],
                SchtasksTimeout,
                cancellationToken).ConfigureAwait(false);

            return result.Status switch
            {
                ProcessRunStatus.Started when result.ExitCode == 0 => StartupTaskState.Present,
                ProcessRunStatus.Started => StartupTaskState.Missing,
                _ => StartupTaskState.Invalid
            };
        }
        catch
        {
            return StartupTaskState.Invalid;
        }
    }

    internal async Task<(bool Success, string Message)> RemoveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var queryResult = await _processRunner.RunAsync(
                SchtasksFullPath,
                ["/Query", "/TN", TaskName],
                SchtasksTimeout,
                cancellationToken).ConfigureAwait(false);

            if (queryResult.Status == ProcessRunStatus.Started && queryResult.ExitCode != 0)
            {
                return (true, string.Empty);
            }

            if (queryResult.Status != ProcessRunStatus.Started)
            {
                var failMsg = queryResult.Status switch
                {
                    ProcessRunStatus.TimedOut => "查询计划任务超时",
                    ProcessRunStatus.StartFailed => "无法启动 schtasks.exe",
                    _ => "查询计划任务失败"
                };
                return (false, failMsg);
            }

            var deleteResult = await _processRunner.RunAsync(
                SchtasksFullPath,
                ["/Delete", "/TN", TaskName, "/F"],
                SchtasksTimeout,
                cancellationToken).ConfigureAwait(false);

            if (deleteResult.Status == ProcessRunStatus.Started && deleteResult.ExitCode == 0)
            {
                return (true, string.Empty);
            }

            var message = deleteResult.Status switch
            {
                ProcessRunStatus.Started => $"删除失败（退出码 {deleteResult.ExitCode}）",
                ProcessRunStatus.TimedOut => "删除操作超时",
                ProcessRunStatus.StartFailed => "无法启动 schtasks.exe",
                _ => "删除操作失败"
            };

            return (false, message);
        }
        catch (Exception ex)
        {
            return (false, $"删除异常：{ex.Message}");
        }
    }

    internal async Task<(bool Success, string Message)> CreateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return (false, "无法获取程序路径");
            }

            var result = await _processRunner.RunAsync(
                SchtasksFullPath,
                ["/Create", "/TN", TaskName, "/TR", exePath, "/SC", "ONLOGON", "/RL", "HIGHEST", "/F"],
                SchtasksTimeout,
                cancellationToken).ConfigureAwait(false);

            if (result.Status == ProcessRunStatus.Started && result.ExitCode == 0)
            {
                return (true, string.Empty);
            }

            var message = result.Status switch
            {
                ProcessRunStatus.Started => $"创建失败（退出码 {result.ExitCode}）",
                ProcessRunStatus.TimedOut => "创建操作超时",
                ProcessRunStatus.StartFailed => "无法启动 schtasks.exe",
                _ => "创建操作失败"
            };

            return (false, message);
        }
        catch (Exception ex)
        {
            return (false, $"创建异常：{ex.Message}");
        }
    }

    private static string SchtasksFullPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), SchtasksExe);
}

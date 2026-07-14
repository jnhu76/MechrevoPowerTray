using System.Diagnostics;

namespace MechrevoPowerTray.Services;

internal sealed class StartupTaskService
{
    private const string TaskName = "MechrevoPowerTray";

    internal bool IsEnabled()
    {
        var result = RunSchtasks("/Query", "/TN", TaskName);
        return result.ExitCode == 0;
    }

    internal (bool Success, string Message) SetEnabled(bool enabled)
    {
        if (enabled)
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return (false, "无法确定当前 EXE 路径。");
            }

            var taskCommand = $"\"{executable}\"";

            var result = RunSchtasks(
                "/Create",
                "/TN", TaskName,
                "/TR", taskCommand,
                "/SC", "ONLOGON",
                "/RL", "HIGHEST",
                "/F");

            return result.ExitCode == 0
                ? (true, "已创建登录启动计划任务。")
                : (false, $"创建计划任务失败：{result.Error}");
        }

        var deleteResult = RunSchtasks(
            "/Delete",
            "/TN", TaskName,
            "/F");

        if (deleteResult.ExitCode == 0 ||
            deleteResult.Error.Contains("cannot find", StringComparison.OrdinalIgnoreCase) ||
            deleteResult.Error.Contains("找不到", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "已关闭登录启动。");
        }

        return (false, $"删除计划任务失败：{deleteResult.Error}");
    }

    private static ProcessResult RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessResult(-1, string.Empty, "无法启动 schtasks.exe。");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        string Error);
}

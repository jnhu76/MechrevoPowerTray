using System.Diagnostics;

namespace MechrevoPowerTray.Services;

internal sealed class ProcessRunner : IProcessRunner, IDisposable
{
    private const int MaxStderrLength = 2000;
    private const int StreamDrainTimeoutMs = 3000;
    private const int KillWaitMs = 5000;

    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            return new ProcessRunResult(ProcessRunStatus.StartFailed, null, string.Empty, ex.Message);
        }

        if (process is null)
        {
            return new ProcessRunResult(ProcessRunStatus.StartFailed, null, string.Empty, "进程启动失败。");
        }

        try
        {
            var stdoutTask = ReadStreamAsync(process.StandardOutput, cancellationToken);
            var stderrTask = ReadStreamAsync(process.StandardError, cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);

            if (completedTask != waitTask)
            {
                return await HandleTimeout(process, stdoutTask, stderrTask).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = await DrainTask(stdoutTask).ConfigureAwait(false);
            var stderr = TruncateStderr(await DrainTask(stderrTask).ConfigureAwait(false));

            return new ProcessRunResult(ProcessRunStatus.Started, process.ExitCode, stdout, stderr);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<string> ReadStreamAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<ProcessRunResult> HandleTimeout(
        Process process,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromMilliseconds(KillWaitMs))
                .ConfigureAwait(false);
        }
        catch
        {
        }

        var stdout = await DrainTask(stdoutTask).ConfigureAwait(false);
        var stderr = TruncateStderr(await DrainTask(stderrTask).ConfigureAwait(false));

        return new ProcessRunResult(ProcessRunStatus.TimedOut, null, stdout, stderr);
    }

    private static async Task<string> DrainTask(Task<string> task)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromMilliseconds(StreamDrainTimeoutMs)).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TruncateStderr(string stderr)
    {
        if (stderr.Length <= MaxStderrLength)
        {
            return stderr;
        }

        return stderr[..MaxStderrLength] + "…";
    }

    public void Dispose()
    {
    }
}

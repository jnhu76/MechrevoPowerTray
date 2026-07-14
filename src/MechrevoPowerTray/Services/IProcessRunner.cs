namespace MechrevoPowerTray.Services;

internal enum ProcessRunStatus
{
    Started,
    TimedOut,
    StartFailed
}

internal sealed record ProcessRunResult(
    ProcessRunStatus Status,
    int? ExitCode,
    string Stdout,
    string Stderr);

internal interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

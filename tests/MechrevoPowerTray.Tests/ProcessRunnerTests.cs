using System.Diagnostics;
using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class ProcessRunnerTests : IDisposable
{
    private readonly ProcessRunner _runner = new();
    private readonly string _testHelperDir;

    public ProcessRunnerTests()
    {
        _testHelperDir = Path.Combine(Path.GetTempPath(), "MPT_ProcessRunnerTests");
        Directory.CreateDirectory(_testHelperDir);
    }

    [Fact]
    public async Task ProcessRunner_ReadsStdoutAndStderrConcurrently()
    {
        var helperPath = Path.Combine(_testHelperDir, "dual_stream_test.cmd");
        var cmdContent = """
            @echo off
            echo STDOUT_LINE_1
            echo STDERR_LINE_1 1>&2
            echo STDOUT_LINE_2
            echo STDERR_LINE_2 1>&2
            """;
        await File.WriteAllTextAsync(helperPath, cmdContent);

        var result = await _runner.RunAsync(
            helperPath,
            [],
            TimeSpan.FromSeconds(10));

        Assert.Equal(ProcessRunStatus.Started, result.Status);
        Assert.Contains("STDOUT_LINE_1", result.Stdout);
        Assert.Contains("STDOUT_LINE_2", result.Stdout);
        Assert.Contains("STDERR_LINE_1", result.Stderr);
        Assert.Contains("STDERR_LINE_2", result.Stderr);
    }

    [Fact]
    public async Task ProcessRunner_Timeout_KillsProcess()
    {
        var helperPath = Path.Combine(_testHelperDir, "sleep_test.cmd");
        var cmdContent = """
            @echo off
            echo STARTING
            ping -n 10 127.0.0.1 >nul
            echo FINISHED
            """;
        await File.WriteAllTextAsync(helperPath, cmdContent);

        var result = await _runner.RunAsync(
            helperPath,
            [],
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task ProcessRunner_StartFailed_ReturnsStartFailed()
    {
        var result = await _runner.RunAsync(
            @"C:\does_not_exist\nonexistent.exe",
            [],
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProcessRunStatus.StartFailed, result.Status);
    }

    [Fact]
    public async Task ProcessRunner_ExitCode_IsCaptured()
    {
        var helperPath = Path.Combine(_testHelperDir, "exit42_test.cmd");
        var cmdContent = """
            @echo on
            exit /b 42
            """;
        await File.WriteAllTextAsync(helperPath, cmdContent);

        var result = await _runner.RunAsync(
            helperPath,
            [],
            TimeSpan.FromSeconds(10));

        Assert.Equal(ProcessRunStatus.Started, result.Status);
        Assert.Equal(42, result.ExitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testHelperDir, recursive: true); } catch { }
        _runner.Dispose();
    }
}

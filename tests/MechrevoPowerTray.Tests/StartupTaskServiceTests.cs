using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public async Task GetState_QueryExit0_ReturnsLegacyPresent()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, "Task exists", string.Empty)));

        var service = new StartupTaskService(runner);
        var state = await service.GetStateAsync();

        Assert.Equal(StartupTaskState.LegacyPresent, state);
    }

    [Fact]
    public async Task GetState_QueryNonzero_ReturnsMissing()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 1, string.Empty, "ERROR: Task does not exist")));

        var service = new StartupTaskService(runner);
        var state = await service.GetStateAsync();

        Assert.Equal(StartupTaskState.Missing, state);
    }

    [Fact]
    public async Task GetState_ProcessStartFailure_ReturnsInvalid()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.StartFailed, null, string.Empty, "File not found")));

        var service = new StartupTaskService(runner);
        var state = await service.GetStateAsync();

        Assert.Equal(StartupTaskState.Invalid, state);
    }

    [Fact]
    public async Task GetState_Timeout_ReturnsInvalid()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.TimedOut, null, string.Empty, string.Empty)));

        var service = new StartupTaskService(runner);
        var state = await service.GetStateAsync();

        Assert.Equal(StartupTaskState.Invalid, state);
    }

    [Fact]
    public async Task Remove_Missing_IsIdempotentSuccess()
    {
        var callCount = 0;
        var runner = new StubProcessRunner((fileName, arguments, _, _) =>
        {
            callCount++;
            // First call = query - task does not exist
            if (callCount == 1)
            {
                return Task.FromResult(new ProcessRunResult(
                    ProcessRunStatus.Started, 1, string.Empty, "ERROR: Task does not exist"));
            }

            // Should not reach delete
            return Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, string.Empty, string.Empty));
        });

        var service = new StartupTaskService(runner);
        var (success, message) = await service.RemoveAsync();

        Assert.True(success);
        Assert.Equal(string.Empty, message);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Remove_LegacyPresent_DeleteExit0_ReturnsSuccess()
    {
        var callCount = 0;
        var runner = new StubProcessRunner((fileName, arguments, _, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new ProcessRunResult(
                    ProcessRunStatus.Started, 0, "Task exists", string.Empty));
            }

            return Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, string.Empty, string.Empty));
        });

        var service = new StartupTaskService(runner);
        var (success, message) = await service.RemoveAsync();

        Assert.True(success);
        Assert.Equal(string.Empty, message);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Remove_DeleteNonzero_ReturnsFailure()
    {
        var callCount = 0;
        var runner = new StubProcessRunner((fileName, arguments, _, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new ProcessRunResult(
                    ProcessRunStatus.Started, 0, "Task exists", string.Empty));
            }

            // Delete fails
            return Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 1, string.Empty, "Access denied"));
        });

        var service = new StartupTaskService(runner);
        var (success, message) = await service.RemoveAsync();

        Assert.False(success);
        Assert.Contains("1", message);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Remove_DoesNotMatchLocalizedErrorText()
    {
        var runner = new StubProcessRunner((fileName, arguments, _, _) =>
        {
            // Both query and delete return original error texts
            // Service must NOT match on localized strings
            return Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 1, string.Empty,
                "找不到指定的任务"));
        });

        var service = new StartupTaskService(runner);
        var (success, message) = await service.RemoveAsync();

        Assert.True(success);
        Assert.Equal(string.Empty, message);
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ProcessRunResult>> _handler;

        internal StubProcessRunner(
            Func<string, IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ProcessRunResult>> handler)
        {
            _handler = handler;
        }

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return _handler(fileName, arguments, timeout, cancellationToken);
        }
    }
}

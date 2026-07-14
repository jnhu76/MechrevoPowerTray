using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public async Task GetState_QueryExit0_ReturnsPresent()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, "Task exists", string.Empty)));

        var service = new StartupTaskService(runner);
        var state = await service.GetStateAsync();

        Assert.Equal(StartupTaskState.Present, state);
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
    public async Task Remove_Present_DeleteExit0_ReturnsSuccess()
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

    [Fact]
    public async Task Create_Exit0_ReturnsSuccess()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, "SUCCESS: Task created", string.Empty)));

        var service = new StartupTaskService(runner);
        var (success, message) = await service.CreateAsync();

        Assert.True(success);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public async Task Create_NonzeroExit_ReturnsFailure()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 1, string.Empty, "ERROR: Access denied")));

        var service = new StartupTaskService(runner);
        var (success, message) = await service.CreateAsync();

        Assert.False(success);
        Assert.Contains("1", message);
    }

    [Fact]
    public async Task Create_Timeout_ReturnsFailure()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.TimedOut, null, string.Empty, string.Empty)));

        var service = new StartupTaskService(runner);
        var (success, message) = await service.CreateAsync();

        Assert.False(success);
        Assert.Contains("超时", message);
    }

    [Fact]
    public async Task Create_StartFailed_ReturnsFailure()
    {
        var runner = new StubProcessRunner((_, _, _, _) =>
            Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.StartFailed, null, string.Empty, "File not found")));

        var service = new StartupTaskService(runner);
        var (success, message) = await service.CreateAsync();

        Assert.False(success);
        Assert.Contains("schtasks.exe", message);
    }

    [Fact]
    public void BuildTaskXml_PathWithSpaces_NotSplit()
    {
        var xml = StartupTaskService.BuildTaskXml(
            "S-1-5-21-1234",
            @"C:\Program Files\MechrevoPowerTray\MechrevoPowerTray.exe");

        Assert.Contains("<Command>C:\\Program Files\\MechrevoPowerTray\\MechrevoPowerTray.exe</Command>", xml);
        Assert.DoesNotContain("<Command>C:\\Program</Command>", xml);
        Assert.Contains("<UserId>S-1-5-21-1234</UserId>", xml);
        Assert.Contains("<LogonType>InteractiveToken</LogonType>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>", xml);
    }

    [Fact]
    public void BuildTaskXml_ContainsLogonTrigger()
    {
        var xml = StartupTaskService.BuildTaskXml(
            "S-1-5-21-1234",
            @"D:\MechrevoPowerTray.exe");

        Assert.Contains("<LogonTrigger>", xml);
        Assert.Contains("<Enabled>true</Enabled>", xml);
    }

    [Fact]
    public async Task Create_WritesXmlFile_WithFullPath_NotSplit()
    {
        string? capturedXmlContent = null;

        var runner = new StubProcessRunner((_, arguments, _, _) =>
        {
            // Find the /XML argument (the temp file path)
            for (var i = 0; i < arguments.Count; i++)
            {
                if (arguments[i] == "/XML" && i + 1 < arguments.Count)
                {
                    var xmlPath = arguments[i + 1];
                    if (File.Exists(xmlPath))
                    {
                        capturedXmlContent = File.ReadAllText(xmlPath);
                    }
                }
            }

            return Task.FromResult(new ProcessRunResult(
                ProcessRunStatus.Started, 0, "SUCCESS: Task created", string.Empty));
        });

        var service = new StartupTaskService(runner);
        var (success, _) = await service.CreateAsync();

        Assert.True(success);
        Assert.NotNull(capturedXmlContent);

        var exePath = Environment.ProcessPath ?? string.Empty;
        Assert.Contains($"<Command>{exePath}</Command>", capturedXmlContent!);

        var commandStart = capturedXmlContent!.IndexOf("<Command>", StringComparison.Ordinal);
        var commandEnd = capturedXmlContent.IndexOf("</Command>", StringComparison.Ordinal);
        Assert.True(commandStart >= 0 && commandEnd > commandStart,
            "XML 应包含完整的 <Command> 元素且路径未被空格拆分。");
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

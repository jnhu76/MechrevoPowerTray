using MechrevoPowerTray.Models;
using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class OemPowerModeServiceTests
{
    [Fact]
    public async Task RejectsMode0BeforeBackend()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, true, null),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync((OemPowerMode)0);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Contains("未知 OEM 参数", result.Message);
    }

    [Fact]
    public async Task RejectsMode4BeforeBackend()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, true, null),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync((OemPowerMode)4);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Contains("未知 OEM 参数", result.Message);
    }

    [Fact]
    public async Task RejectsMode255BeforeBackend()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, true, null),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync((OemPowerMode)255);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Contains("未知 OEM 参数", result.Message);
    }

    [Fact]
    public async Task ZeroActiveInstances_IsRejected()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, true, "没有找到 Active 实例。"),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Equal(0, result.ActiveInstanceCount);
    }

    [Fact]
    public async Task MultipleActiveInstances_IsRejectedWithoutInvoke()
    {
        var invokeCalled = false;
        var backend = new StubBackend(
            new OemBackendProbeResult(3, true, null),
            new OemBackendInvokeResult(false, null, false, false, null))
        {
            OnInvoke = () => invokeCalled = true
        };

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Equal(3, result.ActiveInstanceCount);
        Assert.False(invokeCalled, "Invoke must not be called when multiple instances exist");
    }

    [Fact]
    public async Task ExactlyOneInstance_InvokesExactlyOnce()
    {
        var invokeCount = 0;
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, 0, false, false, null))
        {
            OnInvoke = () => invokeCount++
        };

        var service = new OemPowerModeService(backend);
        await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(1, invokeCount);
    }

    [Fact]
    public async Task NullOutput_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
    }

    [Fact]
    public async Task MissingReturnValue_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
        Assert.Contains("未返回 ReturnValue", result.Message);
    }

    [Fact]
    public async Task NullReturnValue_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
    }

    [Fact]
    public async Task InvalidReturnValueType_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
    }

    [Fact]
    public async Task ReturnValueZero_IsAccepted()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, 0, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Accepted, result.Outcome);
        Assert.Equal(OemPowerMode.Balanced, result.RequestedMode);
    }

    [Fact]
    public async Task ReturnValueNonzero_IsRejected()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, 5, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
        Assert.Equal(5u, result.ReturnValue);
    }

    [Fact]
    public async Task InvokeTimeout_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, true, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
        Assert.Contains("超时", result.Message);
        Assert.Contains("请勿立即重复切换", result.Message);
    }

    [Fact]
    public async Task InvokeUnknownException_IsIndeterminate()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, false, true, "未知错误"));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Indeterminate, result.Outcome);
        Assert.Contains("请勿立即重复切换", result.Message);
    }

    [Fact]
    public async Task QueryFailureBeforeInvoke_IsRejected()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, false, "WMI 枚举失败"),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Balanced);

        Assert.Equal(OemSwitchOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task Accepted_UpdatesLastAcceptedMode()
    {
        var store = new InMemorySettingsStore();
        var settings = store.Load();

        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, 0, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Performance);

        Assert.True(result.IsAccepted);
        Assert.Equal(OemPowerMode.Performance, result.RequestedMode);
    }

    [Fact]
    public async Task Rejected_DoesNotUpdateLastAcceptedMode()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(0, true, "没有 Active 实例。"),
            new OemBackendInvokeResult(false, null, false, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Quiet);

        Assert.NotEqual(OemSwitchOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public async Task Indeterminate_DoesNotUpdateLastAcceptedMode()
    {
        var backend = new StubBackend(
            new OemBackendProbeResult(1, true, null),
            new OemBackendInvokeResult(true, null, true, false, null));

        var service = new OemPowerModeService(backend);
        var result = await service.SetModeAsync(OemPowerMode.Quiet);

        Assert.NotEqual(OemSwitchOutcome.Accepted, result.Outcome);
    }

    private sealed class StubBackend : IOemPowerModeBackend
    {
        private readonly OemBackendProbeResult _probeResult;
        private readonly OemBackendInvokeResult _invokeResult;

        internal Action? OnInvoke { get; set; }

        internal StubBackend(
            OemBackendProbeResult probeResult,
            OemBackendInvokeResult invokeResult)
        {
            _probeResult = probeResult;
            _invokeResult = invokeResult;
        }

        public OemBackendProbeResult ProbeActiveInstances() => _probeResult;

        public OemBackendInvokeResult InvokeSingleInstance(byte value)
        {
            OnInvoke?.Invoke();
            return _invokeResult;
        }

        public void Dispose()
        {
        }
    }

    private sealed class InMemorySettingsStore
    {
        private AppSettings? _saved;

        internal AppSettings Load() => _saved ?? new AppSettings();

        internal void Save(AppSettings settings) => _saved = settings;
    }
}

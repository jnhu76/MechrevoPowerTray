namespace MechrevoPowerTray.Services;

internal sealed record OemBackendProbeResult(
    int ActiveInstanceCount,
    bool Succeeded,
    string? ErrorMessage);

internal sealed record OemBackendInvokeResult(
    bool InvokeAttempted,
    uint? ReturnValue,
    bool TimedOut,
    bool HadException,
    string? ErrorMessage);

internal interface IOemPowerModeBackend : IDisposable
{
    OemBackendProbeResult ProbeActiveInstances();

    OemBackendInvokeResult InvokeSingleInstance(byte value);
}

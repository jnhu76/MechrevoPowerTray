namespace MechrevoPowerTray.Models;

internal enum OemSwitchOutcome
{
    Rejected,
    Indeterminate,
    Accepted
}

internal sealed record OemModeSwitchResult(
    OemSwitchOutcome Outcome,
    OemPowerMode RequestedMode,
    string Message,
    uint? ReturnValue = null,
    int ActiveInstanceCount = 0)
{
    public bool IsAccepted => Outcome == OemSwitchOutcome.Accepted;
}

internal sealed record PowerPlanSwitchResult(
    bool Success,
    string Message,
    uint NativeErrorCode = 0);

internal sealed record DiagnosticResult(
    bool Success,
    string Summary,
    string Details);

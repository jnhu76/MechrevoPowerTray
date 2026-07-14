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

internal sealed record CombinedModeSwitchResult(
    OemPowerMode RequestedMode,
    OemModeSwitchResult OemFirmware,
    PowerPlanSwitchResult Plan,
    PowerPlanSwitchResult Overlay)
{
    public bool OemAccepted => OemFirmware.IsAccepted;

    public bool AllSuccessful => OemFirmware.IsAccepted && Plan.Success && Overlay.Success;

    public string CombinedMessage
    {
        get
        {
            var parts = new List<string> { OemFirmware.Message };

            if (!Plan.Success)
                parts.Add($"电源计划：{Plan.Message}");

            if (!Overlay.Success)
                parts.Add($"叠加方案：{Overlay.Message}");

            return string.Join(Environment.NewLine, parts);
        }
    }
}

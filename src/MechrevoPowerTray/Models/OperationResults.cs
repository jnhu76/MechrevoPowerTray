namespace MechrevoPowerTray.Models;

internal sealed record OemModeSwitchResult(
    bool Success,
    string Message,
    uint? ReturnValue = null,
    int MatchingInstanceCount = 0);

internal sealed record PowerPlanSwitchResult(
    bool Success,
    string Message,
    uint NativeErrorCode = 0);

internal sealed record DiagnosticResult(
    bool Success,
    string Summary,
    string Details);

using System.Management;
using MechrevoPowerTray.Models;

namespace MechrevoPowerTray.Services;

internal sealed class OemPowerModeService
{
    private const string NamespacePath = @"\\.\root\wmi";
    private static readonly TimeSpan EnumerationTimeout = TimeSpan.FromSeconds(8);

    private readonly IOemPowerModeBackend? _backend;

    internal OemPowerModeService()
    {
    }

    internal OemPowerModeService(IOemPowerModeBackend backend)
    {
        _backend = backend;
    }

    internal Task<OemModeSwitchResult> SetModeAsync(
        OemPowerMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!mode.IsWhitelisted())
        {
            return Task.FromResult(new OemModeSwitchResult(
                OemSwitchOutcome.Rejected,
                mode,
                $"拒绝未知 OEM 参数：{(byte)mode}。仅允许 1、2、3。"));
        }

        if (_backend is null)
        {
            return Task.FromResult(new OemModeSwitchResult(
                OemSwitchOutcome.Rejected,
                mode,
                "未配置后端服务。"));
        }

        return Task.Run(() => SetMode(mode), cancellationToken);
    }

    internal Task<DiagnosticResult> DiagnoseAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(Diagnose, cancellationToken);

    private OemModeSwitchResult SetMode(OemPowerMode mode)
    {
        using var backend = _backend!;

        var probe = backend.ProbeActiveInstances();

        if (!probe.Succeeded)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Rejected,
                mode,
                probe.ErrorMessage ?? "WMI 查询失败。",
                ActiveInstanceCount: 0);
        }

        if (probe.ActiveInstanceCount == 0)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Rejected,
                mode,
                "没有找到 Active=TRUE 的 SetOemPowerSwitch 实例。请确认 OEM WMI 提供程序和 BIOS 接口仍然可用。",
                ActiveInstanceCount: 0);
        }

        if (probe.ActiveInstanceCount > 1)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Rejected,
                mode,
                $"发现 {probe.ActiveInstanceCount} 个 Active 实例。期望唯一实例，拒绝写入。",
                ActiveInstanceCount: probe.ActiveInstanceCount);
        }

        var invoke = backend.InvokeSingleInstance((byte)mode);

        if (!invoke.InvokeAttempted)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Indeterminate,
                mode,
                "WMI 调用未执行。",
                ActiveInstanceCount: 1);
        }

        if (invoke.TimedOut)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Indeterminate,
                mode,
                "OEM WMI 调用超时。请求可能已经生效，请勿立即重复切换。",
                ActiveInstanceCount: 1);
        }

        if (invoke.HadException)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Indeterminate,
                mode,
                "OEM WMI 调用异常。" +
                (invoke.ErrorMessage is not null ? $" {invoke.ErrorMessage}" : "") +
                " 请求可能已经生效，请勿立即重复切换。",
                ActiveInstanceCount: 1);
        }

        if (invoke.ReturnValue is null)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Indeterminate,
                mode,
                "OEM WMI 调用未返回 ReturnValue，无法确认结果。请求可能已经生效，请勿立即重复切换。",
                ActiveInstanceCount: 1);
        }

        if (invoke.ReturnValue == 0)
        {
            return new OemModeSwitchResult(
                OemSwitchOutcome.Accepted,
                mode,
                $"OEM 请求“{mode.DisplayName()}”模式已被接受。",
                invoke.ReturnValue,
                1);
        }

        return new OemModeSwitchResult(
            OemSwitchOutcome.Rejected,
            mode,
            $"OEM 方法返回错误码 {invoke.ReturnValue}。",
            invoke.ReturnValue,
            1);
    }

    private static DiagnosticResult Diagnose()
    {
        try
        {
            var scope = CreateDiagnoseScope();
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery("SELECT * FROM SetOemPowerSwitch"));

            using var instances = searcher.Get();

            var total = 0;
            var active = 0;
            var lines = new List<string>();

            foreach (ManagementObject instance in instances)
            {
                using (instance)
                {
                    total++;
                    var isActive = instance.Properties["Active"]?.Value is bool value && value;
                    if (isActive)
                    {
                        active++;
                    }

                    lines.Add(
                        $"实例 {total}: Path={instance.Path?.Path ?? "(unknown)"}, Active={isActive}");
                }
            }

            var success = total > 0 && active > 0;
            var summary = success
                ? $"OEM WMI 正常：共 {total} 个实例，其中 {active} 个 Active。"
                : $"OEM WMI 不完整：共 {total} 个实例，其中 {active} 个 Active。";

            var details = lines.Count == 0
                ? "未枚举到 SetOemPowerSwitch 实例。"
                : string.Join(Environment.NewLine, lines);

            return new DiagnosticResult(success, summary, details);
        }
        catch (Exception ex)
        {
            return new DiagnosticResult(
                false,
                "OEM WMI 诊断失败。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ManagementScope CreateDiagnoseScope()
    {
        var options = new ConnectionOptions
        {
            EnablePrivileges = true,
            Impersonation = ImpersonationLevel.Impersonate,
            Authentication = AuthenticationLevel.PacketPrivacy,
            Timeout = TimeSpan.FromSeconds(8)
        };

        return new ManagementScope(NamespacePath, options);
    }
}

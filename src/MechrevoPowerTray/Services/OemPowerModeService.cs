using System.Globalization;
using System.Management;
using MechrevoPowerTray.Models;

namespace MechrevoPowerTray.Services;

internal sealed class OemPowerModeService
{
    private const string NamespacePath = @"\\.\root\wmi";
    private const string Query =
        "SELECT * FROM SetOemPowerSwitch WHERE Active = TRUE";
    private const string MethodName = "SetOemPowerSwitch";
    private const string InputParameterName = "u8Input";

    internal Task<OemModeSwitchResult> SetModeAsync(
        OemPowerMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!mode.IsWhitelisted())
        {
            return Task.FromResult(
                new OemModeSwitchResult(
                    false,
                    $"拒绝未知 OEM 参数：{(byte)mode}。仅允许 1、2、3。"));
        }

        return Task.Run(() => SetMode(mode), cancellationToken);
    }

    internal Task<DiagnosticResult> DiagnoseAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(Diagnose, cancellationToken);

    private static OemModeSwitchResult SetMode(OemPowerMode mode)
    {
        try
        {
            var scope = CreateScope();
            scope.Connect();

            var options = new System.Management.EnumerationOptions
            {
                ReturnImmediately = false,
                Rewindable = false,
                Timeout = TimeSpan.FromSeconds(8)
            };

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery(Query),
                options);

            using var instances = searcher.Get();

            var count = 0;
            uint? lastReturnValue = null;

            foreach (ManagementObject instance in instances)
            {
                using (instance)
                {
                    count++;

                    using var input = instance.GetMethodParameters(MethodName);
                    input[InputParameterName] = (byte)mode;

                    var invokeOptions = new InvokeMethodOptions
                    {
                        Timeout = TimeSpan.FromSeconds(10)
                    };

                    using var output = instance.InvokeMethod(
                        MethodName,
                        input,
                        invokeOptions);

                    if (output?["ReturnValue"] is not null)
                    {
                        lastReturnValue = Convert.ToUInt32(
                            output["ReturnValue"],
                            CultureInfo.InvariantCulture);

                        if (lastReturnValue != 0)
                        {
                            return new OemModeSwitchResult(
                                false,
                                $"OEM 方法返回错误码 {lastReturnValue}。",
                                lastReturnValue,
                                count);
                        }
                    }
                }
            }

            if (count == 0)
            {
                return new OemModeSwitchResult(
                    false,
                    "没有找到 Active=TRUE 的 SetOemPowerSwitch 实例。请确认 OEM WMI 提供程序和 BIOS 接口仍然可用。");
            }

            return new OemModeSwitchResult(
                true,
                $"已向 {count} 个 OEM WMI 实例请求“{mode.DisplayName()}”模式。",
                lastReturnValue,
                count);
        }
        catch (ManagementException ex)
        {
            return new OemModeSwitchResult(
                false,
                $"WMI 调用失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new OemModeSwitchResult(
                false,
                $"权限不足：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new OemModeSwitchResult(
                false,
                $"切换失败：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static DiagnosticResult Diagnose()
    {
        try
        {
            var scope = CreateScope();
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

    private static ManagementScope CreateScope()
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

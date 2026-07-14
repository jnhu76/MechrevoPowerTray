using System.Globalization;
using System.Management;

namespace MechrevoPowerTray.Services;

internal sealed class WmiOemPowerModeBackend : IOemPowerModeBackend
{
    private const string NamespacePath = @"\\.\root\wmi";
    private const string Query = "SELECT * FROM SetOemPowerSwitch WHERE Active = TRUE";
    private const string MethodName = "SetOemPowerSwitch";
    private const string InputParameterName = "u8Input";
    private static readonly TimeSpan EnumerationTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan InvokeTimeout = TimeSpan.FromSeconds(10);

    private ManagementObject? _cachedInstance;
    private bool _disposed;

    public OemBackendProbeResult ProbeActiveInstances()
    {
        ManagementObjectSearcher? searcher = null;
        ManagementObjectCollection? collection = null;

        try
        {
            var scope = CreateScope();
            scope.Connect();

            var options = new System.Management.EnumerationOptions
            {
                ReturnImmediately = false,
                Rewindable = false,
                Timeout = EnumerationTimeout
            };

            searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery(Query),
                options);

            collection = searcher.Get();

            var instances = collection.Cast<ManagementObject>().ToList();

            foreach (var instance in instances.Skip(1))
            {
                instance.Dispose();
            }

            if (instances.Count == 1)
            {
                _cachedInstance = instances[0];
                return new OemBackendProbeResult(1, true, null);
            }

            if (instances.Count > 0)
            {
                instances[0].Dispose();
            }

            return new OemBackendProbeResult(
                instances.Count,
                true,
                instances.Count == 0
                    ? "没有找到 Active 实例。"
                    : null);
        }
        catch (ManagementException ex)
        {
            return new OemBackendProbeResult(0, false, $"WMI 枚举失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new OemBackendProbeResult(0, false, $"权限不足：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new OemBackendProbeResult(0, false, $"枚举异常：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            searcher?.Dispose();
            collection?.Dispose();
        }
    }

    public OemBackendInvokeResult InvokeSingleInstance(byte value)
    {
        if (_cachedInstance is null)
        {
            return new OemBackendInvokeResult(false, null, false, false, "没有缓存的实例。");
        }

        try
        {
            using var input = _cachedInstance.GetMethodParameters(MethodName);
            input[InputParameterName] = value;

            var invokeOptions = new InvokeMethodOptions
            {
                Timeout = InvokeTimeout
            };

            using var output = _cachedInstance.InvokeMethod(
                MethodName,
                input,
                invokeOptions);

            if (output is null)
            {
                return new OemBackendInvokeResult(true, null, false, false, null);
            }

            var returnValueRaw = output["ReturnValue"];

            if (returnValueRaw is null)
            {
                return new OemBackendInvokeResult(true, null, false, false, null);
            }

            return new OemBackendInvokeResult(
                true,
                Convert.ToUInt32(returnValueRaw, CultureInfo.InvariantCulture),
                false,
                false,
                null);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.Timedout)
        {
            return new OemBackendInvokeResult(true, null, true, false, null);
        }
        catch (ManagementException ex)
        {
            return new OemBackendInvokeResult(true, null, false, true, $"WMI 调用异常：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new OemBackendInvokeResult(true, null, false, true, $"调用异常：{ex.GetType().Name}: {ex.Message}");
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _cachedInstance?.Dispose();
            _disposed = true;
        }
    }
}

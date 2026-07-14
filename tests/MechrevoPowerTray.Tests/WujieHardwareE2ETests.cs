using System.Management;
using MechrevoPowerTray.Models;
using MechrevoPowerTray.Services;
using Xunit;

namespace MechrevoPowerTray.Tests;

public sealed class WujieHardwareE2ETests
{
    [Fact]
    public void ProbeActiveInstances_OnWujieHardware_FindsExactlyOne()
    {
        if (!IsWujie16Pro())
            return;

        using var backend = new WmiOemPowerModeBackend();
        var result = backend.ProbeActiveInstances();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ActiveInstanceCount);
        Assert.NotNull(result.Contract);
        Assert.False(result.Contract.HasOutParameters);
        Assert.False(result.Contract.HasReturnValue);
        Assert.False(result.Contract.IsInputArray);
        Assert.Equal("u8Input", result.Contract.InputParameterName);
    }

    [Fact]
    public void InvokeSingleInstance_OnWujieHardware_DoesNotThrow()
    {
        if (!IsWujie16Pro())
            return;

        using var backend = new WmiOemPowerModeBackend();
        var probe = backend.ProbeActiveInstances();
        Assert.True(probe.Succeeded);
        Assert.Equal(1, probe.ActiveInstanceCount);

        var invoke = backend.InvokeSingleInstance((byte)OemPowerMode.Balanced);
        Assert.True(invoke.InvokeAttempted);
        Assert.False(invoke.TimedOut);
        Assert.False(invoke.HadException);
    }

    [Fact]
    public void Contract_OnWujieHardware_MatchesReport()
    {
        if (!IsWujie16Pro())
            return;

        var scope = new ManagementScope(@"\\.\root\wmi");
        scope.Connect();
        using var searcher = new ManagementObjectSearcher(
            scope,
            new ObjectQuery("SELECT * FROM SetOemPowerSwitch WHERE Active = TRUE"));
        using var coll = searcher.Get();
        ManagementObject? instance = null;
        foreach (ManagementObject obj in coll)
        {
            instance = obj;
            break;
        }

        Assert.NotNull(instance);
        var contract = OemWmiMethodContract.Probe(instance!);
        Assert.NotNull(contract);
        Assert.False(contract.HasOutParameters);
        Assert.False(contract.HasReturnValue);
        Assert.False(contract.IsInputArray);
        Assert.Equal("u8Input", contract.InputParameterName);
    }

    private static bool IsWujie16Pro()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                {
                    var manufacturer = obj["Manufacturer"]?.ToString();
                    var model = obj["Model"]?.ToString();
                    return manufacturer == "MECHREVO" && model == "WUJIE16 Pro";
                }
            }
        }
        catch
        {
        }
        return false;
    }
}
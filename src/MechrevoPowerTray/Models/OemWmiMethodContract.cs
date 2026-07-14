using System.Management;

namespace MechrevoPowerTray.Models;

internal sealed record OemWmiMethodContract(
    bool HasOutParameters,
    bool HasReturnValue,
    bool IsInputArray,
    string InputParameterName)
{
    internal const string MethodName = "SetOemPowerSwitch";
    internal const string InputParamName = "u8Input";

    internal static OemWmiMethodContract Probe(ManagementObject instance)
    {
        using var mgmtClass = new ManagementClass(instance.ClassPath);
        var method = mgmtClass.Methods[MethodName];

        var outParams = method.OutParameters;
        var hasOutParams = outParams is not null && outParams.Properties.Count > 0;
        var hasReturnValue = hasOutParams &&
                             outParams!.Properties["ReturnValue"] is not null;

        var inParams = method.InParameters;
        var isArray = false;
        if (inParams is not null)
        {
            var prop = inParams.Properties[InputParamName];
            isArray = prop?.IsArray ?? false;
        }

        return new OemWmiMethodContract(hasOutParams, hasReturnValue, isArray, InputParamName);
    }
}

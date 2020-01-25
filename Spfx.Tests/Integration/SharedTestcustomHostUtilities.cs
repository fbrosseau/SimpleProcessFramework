using System;
using System.Linq;
using System.Reflection;

public class SharedTestcustomHostUtilities
{
    public const string ExecutableName = "Spfx.TestCustomHostExe";
    public const string StandaloneDllName = "TestCustomHostDll";

    internal static void ValidateProcessEntryPoint()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => IsValidEntryAssembly(a));
        if (asm is null)
            throw new InvalidOperationException("Could not find TestCustomHostExe in the loaded assemblies");

        var entryPoint = asm.GetType("TestCustomHostExe");
        if (!(bool)entryPoint.GetProperty("WasMainCalled").GetValue(null))
            throw new InvalidOperationException("WasMainCalled was not called");
    }

    private static bool IsValidEntryAssembly(Assembly a)
    {
        var an = a.GetName().Name;
        return IsCustomHostAssembly(an);
    }

    internal static bool IsCustomHostAssembly(string assemblyName, bool includeDll = true, bool includeExe = true)
    {
        return (includeExe && assemblyName.StartsWith(ExecutableName)) || (includeDll && assemblyName.StartsWith(StandaloneDllName));
    }
}

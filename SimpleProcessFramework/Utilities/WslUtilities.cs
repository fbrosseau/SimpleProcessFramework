using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spfx.Utilities
{
    internal static class WslUtilities
    {
        internal static readonly string WslExeFullPath = Path.Combine(Environment.SystemDirectory, "wsl.exe");

        internal static string GetWslPath(string fullName)
        {
            if (fullName.EndsWith("\\"))
                fullName = fullName.Substring(0, fullName.Length - 1);

            var cmd = $"{ProcessUtilities.EscapeArg(WslExeFullPath)} wslpath {ProcessUtilities.EscapeArg(Path.GetFullPath(fullName))}";
            var output = ProcessUtilities.ExecAndGetConsoleOutput(cmd, TimeSpan.FromSeconds(30));
            output = output.Trim(' ', '\r', '\t', '\n');
            if (!output.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                output += '/';
            return output;
        }
    }
}

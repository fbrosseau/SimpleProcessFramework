namespace Spfx.Interfaces
{
    public enum ProcessKind
    {
        Default,
        Netfx,
        Netfx32,
        Netcore,
        Netcore32,
        DirectlyInRootProcess,
        AppDomain,
        Wsl
    }

    public static class ProcessKindExtensions
    {
        public static bool IsNetcore(this ProcessKind k)
        {
            return k == ProcessKind.Netcore || k == ProcessKind.Netcore32 || k == ProcessKind.Wsl;
        }

        public static bool IsNetfx(this ProcessKind k)
        {
            return k == ProcessKind.Netfx || k == ProcessKind.Netfx32;
        }

        public static bool IsFakeProcess(this ProcessKind k)
        {
            return k == ProcessKind.AppDomain || k == ProcessKind.DirectlyInRootProcess;
        }

        public static bool Is32Bit(this ProcessKind k)
        {
            return k == ProcessKind.Netfx32 || k == ProcessKind.Netcore32;
        }
    }
}
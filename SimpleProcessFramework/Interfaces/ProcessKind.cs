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

        public static bool IsNetfxProcess(this ProcessKind k)
        {
            return k == ProcessKind.Netfx || k == ProcessKind.Netfx32;
        }

        public static bool IsNetfx(this ProcessKind k)
        {
            switch(k)
            {
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                case ProcessKind.AppDomain:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFakeProcess(this ProcessKind k)
        {
            return !k.IsRealProcess();
        }

        public static bool Is32Bit(this ProcessKind k)
        {
            return k == ProcessKind.Netfx32 || k == ProcessKind.Netcore32;
        }

        public static ProcessKind AsAnyCpu(this ProcessKind k)
        {
            switch (k)
            {
                case ProcessKind.Netcore32:
                    return ProcessKind.Netcore;
                case ProcessKind.Netfx32:
                    return ProcessKind.Netfx;
                default:
                    return k;
            }
        }

        public static bool IsRealProcess(this ProcessKind k)
        {
            switch(k)
            {
                case ProcessKind.Default:
                case ProcessKind.Wsl:
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                    return true;
                default:
                    return false;
            }
        }
    }
}
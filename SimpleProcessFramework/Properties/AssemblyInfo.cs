using System;
using System.Runtime.CompilerServices;
using Spfx.Properties;
using Spfx.Reflection;

[assembly: InternalsVisibleTo("Spfx.Tests" + SimpleProcessFrameworkPublicKey.AssemblyNameSuffix)]
[assembly: InternalsVisibleTo("Spfx.Tests.Netfx" + SimpleProcessFrameworkPublicKey.AssemblyNameSuffix)]
[assembly: InternalsVisibleTo("Spfx.Tests.Netcore" + SimpleProcessFrameworkPublicKey.AssemblyNameSuffix)]
[assembly: InternalsVisibleTo("Spfx.TestApp" + SimpleProcessFrameworkPublicKey.AssemblyNameSuffix)]
[assembly: InternalsVisibleTo(DynamicCodeGenModule.DynamicModuleAssemblyIdentity)]

namespace Spfx.Properties
{
    internal static class SimpleProcessFrameworkPublicKey
    {
        public const string PublicKeyString = "00240000048000009400000006020000002400005253413100040000010001009D922C098000222A2C82E657A2682441267EA12A1A4E1451AA4AAD1AE7C95D718720A64352E9FB843E2C85A39F33BBE842A4605624B19AF3B92CCC4BADAD77D753A3FC88B79C9219AED8ACAB65A0EEB84FFB710DA2F3281E20AA63A2C5C0172E87139F97E7E25AFEF47B4BF52405401ACE03DCA7C8C440762A904155E35D40E5";
        public const string AssemblyNameSuffix = ", PublicKey = " + PublicKeyString;
        public static byte[] PublicKeyBytes => (byte[])s_publicKeyBytes.Clone();

        private static readonly byte[] s_publicKeyBytes = GetBytes();

        private static byte[] GetBytes()
        {
            byte[] bytes = new byte[PublicKeyString.Length / 2];
            for (int i = 0; i < PublicKeyString.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(PublicKeyString.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
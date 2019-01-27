﻿using Spfx.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spfx.Tests" + SimpleProcessFrameworkPublicKey.Value)]
[assembly: InternalsVisibleTo("Spfx.TestApp" + SimpleProcessFrameworkPublicKey.Value)]
[assembly: InternalsVisibleTo(DynamicCodeGenModule.DynamicModuleAssemblyIdentity)]

internal static class SimpleProcessFrameworkPublicKey
{
    public const string Value = ", PublicKey = 00240000048000009400000006020000002400005253413100040000010001009D922C098000222A2C82E657A2682441267EA12A1A4E1451AA4AAD1AE7C95D718720A64352E9FB843E2C85A39F33BBE842A4605624B19AF3B92CCC4BADAD77D753A3FC88B79C9219AED8ACAB65A0EEB84FFB710DA2F3281E20AA63A2C5C0172E87139F97E7E25AFEF47B4BF52405401ACE03DCA7C8C440762A904155E35D40E5";
}
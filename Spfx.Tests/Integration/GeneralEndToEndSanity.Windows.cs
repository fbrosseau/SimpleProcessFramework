#if !UNIX_TESTS_BUILD_ONLY

using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Spfx.Tests.Integration
{
    public partial class GeneralEndToEndSanity
    {
#if NETFRAMEWORK
        [Test/*, Parallelizable*/]
        [Category("NetfxHost-Only"), Category("Netfx-Only"), Category("Windows-Only")]
        public void AppDomainCallbackToOtherProcess()
        {
            if (!HostFeaturesHelper.IsWindows || !HostFeaturesHelper.LocalProcessKind.IsNetfx())
                Assert.Ignore("AppDomains not supported");
            TestCallback(ProcessKind.AppDomain, callbackInMaster: false);
        }
#endif
    }
}

#endif

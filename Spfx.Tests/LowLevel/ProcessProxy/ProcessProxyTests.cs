using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Messages;

namespace Spfx.Tests.LowLevel.CodeGen
{
    [TestFixture, Parallelizable]
    public class ProcessProxyTests : CommonTestClass
    {
        [Test]
        public void ProcessProxy_DefaultCtor_Success()
        {
            new ProcessProxy();
        }
    }
}
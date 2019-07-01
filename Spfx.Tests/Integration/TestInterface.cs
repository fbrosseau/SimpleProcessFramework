using Spfx.Interfaces;
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server;
using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    public interface ITestInterface
    {
        Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow = default, TimeSpan delay = default, CancellationToken ct = default, string exceptionText = null);
        Task<string> GetActualProcessName();
        Task<int> GetPointerSize();
        Task<string> GetEnvironmentVariable(string key);
        Task<int> Callback(string uri, int num);
        Task<int> Echo(int val);
        Task<ProcessKind> GetRealProcessKind();
        Task<OsKind> GetOsKind();
        Task<Version> GetNetCoreVersion();
        Task<int> GetProcessId();
        Task<bool> IsWsl();
    }

    internal class TestInterface : AbstractProcessEndpoint, ITestInterface
    {
        public ILogger Logger { get; private set; }

        protected override Task InitializeAsync()
        {
            Logger = ParentProcess.DefaultTypeResolver.GetLogger(GetType(), true);
            Logger.Info?.Trace("InitializeAsync");
            return base.InitializeAsync();
        }

        protected override void OnDispose()
        {
            if (Logger != null)
            {
                Logger.Info?.Trace("OnDispose");
                Logger.Dispose();
            }
            base.OnDispose();
        }

        protected override Task OnTeardownAsync(CancellationToken ct)
        {
            Logger.Info?.Trace("OnTeardownAsync");
            return base.OnTeardownAsync(ct);
        }

        protected override bool FilterMessage(IInterprocessRequestContext request)
        {
            Logger.Debug?.Trace(request.Request.ToString());
            return base.FilterMessage(request);
        }

        public Task<int> Callback(string uri, int num)
        {
            return ParentProcess.ClusterProxy.CreateInterface<ICallbackInterface>(uri).Double(num);
        }

        public Task<int> Echo(int val)
        {
            return Task.FromResult(val);
        }

        public Task<string> GetActualProcessName()
        {
            return Task.FromResult(Process.GetCurrentProcess().ProcessName);
        }

        public Task<Version> GetNetCoreVersion()
        {
            return Task.FromResult(HostFeaturesHelper.NetcoreVersion);
        }

        public async Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow, TimeSpan delay, CancellationToken ct, string exceptionText = null)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            if (exceptionToThrow != null)
            {
                ThrowException_ThisMethodNameShouldBeInExceptionCallstack(exceptionToThrow, exceptionText);
            }

            return new DummyReturn
            {
                DummyValue = DummyReturn.ExpectedDummyValue
            };
        }

        public static readonly string ThrowingMethodName = nameof(ThrowException_ThisMethodNameShouldBeInExceptionCallstack);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowException_ThisMethodNameShouldBeInExceptionCallstack(ReflectedTypeInfo exceptionToThrow, string exceptionText)
        {
            throw (Exception)Activator.CreateInstance(exceptionToThrow.ResolvedType, new object[] { exceptionText ?? "<no exception text>" });
        }

        public Task<string> GetEnvironmentVariable(string key) => Task.FromResult(Environment.GetEnvironmentVariable(key));
        public Task<OsKind> GetOsKind() => Task.FromResult(HostFeaturesHelper.LocalMachineOsKind);
        public Task<int> GetPointerSize() => Task.FromResult(IntPtr.Size);
        public Task<ProcessKind> GetRealProcessKind() => Task.FromResult(HostFeaturesHelper.LocalProcessKind);
        public Task<int> GetProcessId() => Task.FromResult(Process.GetCurrentProcess().Id);
        public Task<bool> IsWsl() => Task.FromResult(HostFeaturesHelper.IsInsideWsl);
    }
}
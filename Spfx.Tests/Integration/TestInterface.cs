using Spfx.Interfaces;
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using NUnit.Framework;

namespace Spfx.Tests.Integration
{
    public interface ITestInterface
    {
        Task<TestReturnValue> GetDummyValue(ReflectedTypeInfo exceptionToThrow = default, TimeSpan delay = default, CancellationToken ct = default, string exceptionText = null);
        Task<string> GetActualProcessName();
        Task<int> GetPointerSize();
        Task<string> GetEnvironmentVariable(string key);
        Task<int> Callback(string uri, int num);
        Task<int> Echo(int val);
        Task<ProcessKind> GetRealProcessKind();
        Task<OsKind> GetOsKind();
        Task<Version> GetNetCoreVersion();
        Task<string> GetLongFormFrameworkDescription(); // RuntimeInformation.FrameworkDescription
        Task<int> GetProcessId();
        Task<bool> IsWsl();
        Task ValidateCustomProcessEntryPoint();
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

        protected override ValueTask OnTeardownAsync(CancellationToken ct)
        {
            Logger.Info?.Trace("OnTeardownAsync");
            return base.OnTeardownAsync(ct);
        }

        protected override bool FilterMessage(IInterprocessRequestContext request)
        {
            Logger.Debug?.Trace(request.Request.GetTinySummaryString());
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
            return Task.FromResult(NetcoreHelper.CurrentProcessNetcoreVersion);
        }

        public async Task<TestReturnValue> GetDummyValue(ReflectedTypeInfo exceptionToThrow, TimeSpan delay, CancellationToken ct, string exceptionText = null)
        {
            try
            {
                if (delay != TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                if (exceptionToThrow != null)
                {
                    ThrowException_ThisMethodNameShouldBeInExceptionCallstack(exceptionToThrow, exceptionText);
                }

                return new TestReturnValue
                {
                    DummyValue = TestReturnValue.ExpectedDummyValue
                };
            }
            catch (Exception ex)
            {
                Logger.Info?.Trace(ex, $"Throwing {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public static readonly string ThrowingMethodName = nameof(ThrowException_ThisMethodNameShouldBeInExceptionCallstack);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowException_ThisMethodNameShouldBeInExceptionCallstack(ReflectedTypeInfo exceptionToThrow, string exceptionText)
        {
            throw (Exception)Activator.CreateInstance(exceptionToThrow.ResolvedType, exceptionText ?? "<no exception text>");
        }

        public Task<string> GetEnvironmentVariable(string key) => Task.FromResult(Environment.GetEnvironmentVariable(key));
        public Task<OsKind> GetOsKind() => Task.FromResult(HostFeaturesHelper.LocalMachineOsKind);
        public Task<int> GetPointerSize() => Task.FromResult(IntPtr.Size);
        public Task<ProcessKind> GetRealProcessKind() => Task.FromResult(HostFeaturesHelper.LocalProcessKind);
        public Task<int> GetProcessId() => Task.FromResult(Process.GetCurrentProcess().Id);
        public Task<bool> IsWsl() => Task.FromResult(HostFeaturesHelper.IsInsideWsl);
        public Task<string> GetLongFormFrameworkDescription() => Task.FromResult(RuntimeInformation.FrameworkDescription);

        public Task ValidateCustomProcessEntryPoint()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.StartsWith("TestCustomHost"));
            if (asm is null)
                throw new InvalidOperationException("Could not find TestCustomHostExe in the loaded assemblies");

            var entryPoint = asm.GetType(nameof(TestCustomHostExe));
            if (!(bool)entryPoint.GetProperty(nameof(TestCustomHostExe.WasMainCalled)).GetValue(null))
                throw new InvalidOperationException("WasMainCalled was not called");

            return Task.CompletedTask;
        }
    }
}
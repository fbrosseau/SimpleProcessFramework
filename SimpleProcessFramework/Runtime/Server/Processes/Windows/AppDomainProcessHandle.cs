#if WINDOWS_BUILD

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal class AppDomainProcessHandle : AbstractExternalProcessTargetHandle
    {
        [Serializable]
        private class AppDomainStartData
        {
            public string InputString { get; set; }
            public IntPtr OutputHandle { get; set; }
            public IntPtr ErrorHandle { get; set; }

            internal void CrossDomainCallback()
            {
                TextWriter RecreateWriter(IntPtr h)
                    => new StreamWriter(new FileStream(new SafeFileHandle(h, true), FileAccess.Write, 4096, false));

                if (OutputHandle != IntPtr.Zero)
                    Console.SetOut(RecreateWriter(OutputHandle));
                if (ErrorHandle != IntPtr.Zero)
                    Console.SetError(RecreateWriter(ErrorHandle));

                __EntryPoint.Run(new StringReader(InputString), isStandaloneProcess: false);
            }
        }

        public AppDomainProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Process> SpawnProcess(IRemoteProcessInitializer handles, CancellationToken ct)
        {
            // So far this is the only place in the entire project where I had to break the abstraction of netstandard.
            // So... for a dying feature like appdomains a little bit of reflection sounds perfectly fine instead of going into a
            // multi-targeting nightmare
            var setupType = Type.GetType("System.AppDomainSetup");
            var appDomainSetup = Activator.CreateInstance(setupType);
            setupType.GetProperty("ApplicationBase").SetValue(appDomainSetup, PathHelper.CurrentBinFolder.FullName);

            var dom = (AppDomain)typeof(AppDomain).InvokeMember("CreateDomain",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                null,
                null,
                new[] { ProcessUniqueId, null, appDomainSetup });

            await DoProtectedCreateProcess(handles, () => Process.GetCurrentProcess(), ct);

            var consoleRedirector = await WindowsConsoleRedirector.CreateAsync(this);

            PrepareConsoleRedirection(Process.GetCurrentProcess(), ProcessUniqueId);          

            Action callback = new AppDomainStartData
            {
                InputString = handles.PayloadText,
                OutputHandle = consoleRedirector.RemoteProcessOut.DangerousGetHandle(),
                ErrorHandle = consoleRedirector.RemoteProcessErr.DangerousGetHandle()
            }.CrossDomainCallback;

            var callbackType = Type.GetType("System.CrossAppDomainDelegate");
            var typedCallback = Delegate.CreateDelegate(callbackType, callback.Target, callback.Method);

            Task.Run(() =>
            {
                using (consoleRedirector)
                {
                    typeof(AppDomain).InvokeMember("DoCallBack",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                        null,
                        dom,
                        new object[] { typedCallback });

                    consoleRedirector.StartReading();
                }
            }, CancellationToken.None)
                .ContinueWith(t => OnProcessLost("AppDomain callback failed: " + t.ExtractException()), ct, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default)
                .FireAndForget();

            return ExternalProcess;
        }
    }
}

#endif
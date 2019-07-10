using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal class AppDomainProcessHandle : GenericRemoteTargetHandle
    {
        [Serializable]
        private class AppDomainStartData
        {
            public string InputString { get; internal set; }

            internal void CrossDomainCallback()
            {
                __EntryPoint.Run(new StringReader(InputString), isStandaloneProcess: false);
            }
        }

        public AppDomainProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver) 
            : base(info, typeResolver)
        {
        }

        protected override async Task<Process> SpawnProcess(IProcessSpawnPunchHandles handles, CancellationToken ct)
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
                new [] { ProcessUniqueId, null, appDomainSetup });

            await ProcessCreationUtilities.InvokeCreateProcess(() =>
            {
                handles.InitializeInLock();
                handles.HandleProcessCreatedInLock(Process.GetCurrentProcess(), RemotePunchPayload);
            });

            var serializedHandles = handles.FinalizeInitDataAndSerialize(Process.GetCurrentProcess(), RemotePunchPayload);

            Action callback = new AppDomainStartData
            {
                InputString = serializedHandles
            }.CrossDomainCallback;

            var callbackType = Type.GetType("System.CrossAppDomainDelegate");
            var typedCallback = Delegate.CreateDelegate(callbackType, callback.Target, callback.Method);

            Task.Run(() =>
            {
                typeof(AppDomain).InvokeMember("DoCallBack",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                    null,
                    dom,
                    new object[] { typedCallback });
            }, CancellationToken.None)
                .ContinueWith(t => OnProcessLost("AppDomain callback failed: " + t.ExtractException()), ct, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default)
                .FireAndForget();

            return Process.GetCurrentProcess();
        }
    }
}

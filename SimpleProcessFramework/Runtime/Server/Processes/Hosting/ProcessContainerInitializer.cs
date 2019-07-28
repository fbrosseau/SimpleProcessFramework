using Spfx.Utilities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spfx.Reflection;
using Spfx.Interfaces;
using Spfx.Diagnostics.Logging;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal abstract class ProcessContainerInitializer : Disposable
    {
        public ProcessSpawnPunchPayload Payload { get; }
        public ITypeResolver TypeResolver { get; }
        public ILogger Logger { get; }
        public bool InitSucceeded { get; private set; }

        internal static ProcessContainerInitializer Create(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
        {
            if (HostFeaturesHelper.IsWindows)
            {
                if (payload.ProcessKind == ProcessKind.AppDomain)
                    return new AppDomainProcessContainerInitializer(payload, typeResolver);
                return new WindowsProcessContainerInitializer(payload, typeResolver);
            }

            if (payload.ProcessKind == ProcessKind.Wsl)
                return new WslProcessContainerInitializer(payload, typeResolver);

            throw new NotSupportedException();
        }

        protected ProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
        {
            Payload = payload;
            TypeResolver = typeResolver;
            Logger = typeResolver.GetLogger(GetType());
        }

        internal abstract IEnumerable<Task> GetShutdownEvents();

        internal virtual void OnInitSucceeded()
        {
            Logger.Debug?.Trace("OnInitSucceeded");
            InitSucceeded = true;
        }

        protected override void OnDispose()
        {
            Logger.Debug?.Trace("OnDispose");
            base.OnDispose();
            Logger.Dispose();
        }

        internal abstract ISubprocessConnector CreateConnector(ProcessContainer owner);
    }
}
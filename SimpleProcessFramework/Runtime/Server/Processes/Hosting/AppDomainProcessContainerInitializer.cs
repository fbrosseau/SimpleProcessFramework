using Spfx.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class AppDomainProcessContainerInitializer : WindowsProcessContainerInitializer
    {
        public AppDomainProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
            : base(payload, typeResolver)
        {
        }

        internal override IEnumerable<SubprocessShutdownEvent> GetHostShutdownEvents()
            => Enumerable.Empty<SubprocessShutdownEvent>();
    }
}
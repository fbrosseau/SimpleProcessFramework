using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spfx.Reflection;

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
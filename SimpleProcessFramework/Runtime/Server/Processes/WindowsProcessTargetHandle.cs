using Spfx.Interfaces;
using Spfx.Reflection;

namespace Spfx.Runtime.Server.Processes
{
    internal class WindowsProcessTargetHandle : ManagedProcessTargetHandle
    {
        // TODO

        public WindowsProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver) 
            : base(info, typeResolver)
        {
        }
    }
}

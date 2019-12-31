using System.IO;
using System.IO.Pipes;
using System;

namespace Spfx.Runtime.Server.Processes
{
    internal sealed class GenericProcessSpawnPunchHandles : AnonymousPipeProcessSpawnPunchHandles
    {
        public GenericProcessSpawnPunchHandles()
        {
            ReadPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            WritePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        }

        protected override IntPtr GetShutdownHandleForOtherProcess()
        {
            return default;
        }
    }
}

using System.IO;
using System.IO.Pipes;
using System;
using System.Diagnostics;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal sealed class GenericProcessSpawnPunchHandles : AbstractProcessSpawnPunchHandles
    {
        public GenericProcessSpawnPunchHandles()
        {
            ReadPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            WritePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        }

        protected override IntPtr GetShutdownHandleForOtherProcess(Process remoteProcess)
        {
            return default;
        }
    }
}

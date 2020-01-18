#if !NETCOREAPP3_0_PLUS

namespace System.Threading
{
    internal interface IThreadPoolWorkItem
    {
        void Execute();
    }
}

#else

using System.Runtime.CompilerServices;
using System.Threading;

[assembly: TypeForwardedTo(typeof(IThreadPoolWorkItem))]

#endif
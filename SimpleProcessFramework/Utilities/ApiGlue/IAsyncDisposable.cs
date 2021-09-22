#if !NETSTANDARD2_1_PLUS

using System;
using System.Threading.Tasks;

namespace System
{
    internal interface IAsyncDisposable
    {
        ValueTask DisposeAsync();
    }
}
#else

using System;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(IAsyncDisposable))]

#endif
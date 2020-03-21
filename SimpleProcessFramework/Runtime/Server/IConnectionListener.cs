using Spfx.Reflection;
using Spfx.Utilities.Threading;
using System;

namespace Spfx.Runtime.Server
{
    public interface IConnectionListener : IAsyncDestroyable
    {
        void Start(ITypeResolver typeResolver);
    }
}

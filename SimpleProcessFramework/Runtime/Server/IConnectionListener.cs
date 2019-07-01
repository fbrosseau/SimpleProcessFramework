using Spfx.Reflection;
using System;

namespace Spfx.Runtime.Server
{
    public interface IConnectionListener : IDisposable
    {
        void Start(ITypeResolver typeResolver);
    }
}

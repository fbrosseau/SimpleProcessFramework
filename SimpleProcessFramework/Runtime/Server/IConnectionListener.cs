using Spfx.Reflection;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server
{
    public interface IConnectionListener : IAsyncDestroyable
    {
        void Start(ITypeResolver typeResolver);
    }
}

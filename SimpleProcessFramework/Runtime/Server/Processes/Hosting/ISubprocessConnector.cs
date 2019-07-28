using System.Threading;
using System.Threading.Tasks;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal interface ISubprocessConnector : IMessageCallbackChannel, IAsyncDestroyable
    {
        Task InitializeAsync(CancellationToken ct);
    }
}
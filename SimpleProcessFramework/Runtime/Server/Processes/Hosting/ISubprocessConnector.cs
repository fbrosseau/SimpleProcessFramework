using Spfx.Utilities.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal interface ISubprocessConnector : IMessageCallbackChannel, IAsyncDestroyable
    {
        Task InitializeAsync(CancellationToken ct);
    }
}
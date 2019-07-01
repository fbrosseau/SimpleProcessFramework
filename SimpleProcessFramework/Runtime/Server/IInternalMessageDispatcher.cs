using System.Threading;
using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server
{
    public interface IInternalMessageDispatcher
    {
        string LocalProcessUniqueId { get; }
        void ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct);
    }
}
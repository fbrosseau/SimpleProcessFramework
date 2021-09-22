using Spfx.Runtime.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Common
{
    public interface IInterprocessConnection : IDisposable
    {
        event EventHandler ConnectionLost;

        void Initialize();
        ValueTask<object> SerializeAndSendMessage(IInterprocessMessage req, CancellationToken ct = default);
        ValueTask<T> SerializeAndSendMessage<T>(IInterprocessMessage req, CancellationToken ct = default);
    }
}
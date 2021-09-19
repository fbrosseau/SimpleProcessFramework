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
        Task<object> SerializeAndSendMessage(IInterprocessMessage req, CancellationToken ct = default);
        Task<T> SerializeAndSendMessage<T>(IInterprocessMessage req, CancellationToken ct = default);
    }
}
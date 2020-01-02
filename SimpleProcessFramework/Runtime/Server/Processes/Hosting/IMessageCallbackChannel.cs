using Spfx.Runtime.Messages;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal interface IMessageCallbackChannel
    {
        void HandleMessage(string connectionId, IInterprocessMessage msg);
        ValueTask<IInterprocessClientChannel> GetClientInfo(string uniqueId);
    }
}
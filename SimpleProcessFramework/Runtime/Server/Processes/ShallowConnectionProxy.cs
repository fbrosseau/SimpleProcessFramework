using Spfx.Utilities;
using Spfx.Runtime.Messages;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal class ShallowConnectionProxy : IInterprocessClientProxy
    {
        public string UniqueId { get; }

        private readonly IMessageCallbackChannel m_owner;

        public ShallowConnectionProxy(IMessageCallbackChannel owner, string sourceId)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            UniqueId = sourceId;
            m_owner = owner;
        }

        public Task<IInterprocessClientChannel> GetClientInfo()
        {
            return m_owner.GetClientInfo(UniqueId);
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            m_owner.HandleMessage(UniqueId, msg);
        }
    }
}
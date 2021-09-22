using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Hosting;
using Spfx.Utilities;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal class ShallowConnectionProxy : IInterprocessClientProxy
    {
        public string UniqueId { get; }
        public bool IsExternalConnection => UniqueId.StartsWith(InterprocessConnectionId.ExternalConnectionIdPrefix);

        private readonly IMessageCallbackChannel m_owner;

        public ShallowConnectionProxy(IMessageCallbackChannel owner, string sourceId)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            Guard.ArgumentNotNullOrEmpty(sourceId, nameof(sourceId));
            UniqueId = sourceId;
            m_owner = owner;
        }

        public ValueTask<IInterprocessClientChannel> GetClientInfo()
        {
            return m_owner.GetClientInfo(UniqueId);
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            m_owner.HandleMessage(UniqueId, msg);
        }

        public override string ToString() => nameof(ShallowConnectionProxy) + ": " + UniqueId;
    }
}
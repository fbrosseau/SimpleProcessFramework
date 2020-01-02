using Spfx.Runtime.Messages;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal class ConcreteClientProxy : IInterprocessClientProxy
    {
        private readonly IInterprocessClientChannel m_actualChannel;

        public string UniqueId => m_actualChannel.UniqueId;
        public bool IsExternalConnection => UniqueId.StartsWith(InterprocessConnectionId.ExternalConnectionIdPrefix);

        public ConcreteClientProxy(IInterprocessClientChannel actualChannel)
        {
            m_actualChannel = actualChannel;
        }

        public ValueTask<IInterprocessClientChannel> GetClientInfo()
        {
            return new ValueTask<IInterprocessClientChannel>(m_actualChannel);
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            m_actualChannel.SendMessageToClient(msg);
        }

        public override string ToString() => nameof(ConcreteClientProxy) + ": " + UniqueId;
    }
}
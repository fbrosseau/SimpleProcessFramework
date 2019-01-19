using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities;
using System;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class ConcreteClientProxy : IInterprocessClientProxy
    {
        private IInterprocessClientChannel m_actualChannel;

        public long UniqueId => m_actualChannel.UniqueId;

        public ConcreteClientProxy(IInterprocessClientChannel actualChannel)
        {
            m_actualChannel = actualChannel;
        }

        public Task<IInterprocessClientChannel> GetClientInfo()
        {
            return Task.FromResult(m_actualChannel);
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            m_actualChannel.SendMessage(msg);
        }
    }
}
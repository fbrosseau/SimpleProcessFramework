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

        public void SendFailure(long callId, Exception fault)
        {
            m_actualChannel.SendFailure(callId, fault);
        }

        public void SendResponse(long callId, object completion)
        {
            m_actualChannel.SendResponse(callId, completion);
        }
    }

}
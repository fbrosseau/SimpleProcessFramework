using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework
{
    internal class SameProcessConnection : IInterprocessConnection
    {
        public SameProcessConnection()
        {

        }

        public void Dispose()
        {
        }

        public void Initialize()
        {
        }

        public Task<object> SendRequest(IInterprocessRequest req)
        {
            var s = new DefaultBinarySerializer().Serialize(req);
            var req2 = new DefaultBinarySerializer().Deserialize<IInterprocessRequest>(s);
            throw null;
        }
    }

    internal interface IConnectionFactory
    {
        IInterprocessConnection GetConnection(ProcessEndpointAddress destination);
    }

    public class ProcessProxy : IProxiedCallHandler
    {
        private readonly IConnectionFactory m_connectionFactory;

        public ProcessProxy()
            : this(new DefaultConnectionFactory())
        {
        }

        internal ProcessProxy(IConnectionFactory connectionFactory)
        {
            m_connectionFactory = connectionFactory;
        }

        public T CreateInterface<T>(ProcessEndpointAddress address)
        {
            var proxy = ProcessProxyFactory.CreateImplementation<T>();
            proxy.Initialize(this, address);
            return (T)(object)proxy;
        }

        async Task<object> IProxiedCallHandler.ProcessCall(RemoteInvocationRequest req, CancellationToken ct)
        {
            var connection = m_connectionFactory.GetConnection(req.Destination);
            var t = connection.SendRequest(req);

            var ctRegistration = new CancellationTokenRegistration();

            if (ct.CanBeCanceled)
            {
                ct.Register(() => connection.SendRequest(new RemoteCallCancellationRequest
                {
                    CallId = req.CallId,
                    Destination = req.Destination
                }), false);
            }

            using (ctRegistration)
            {
                return await t.ConfigureAwait(false);
            }
        }
    }
}
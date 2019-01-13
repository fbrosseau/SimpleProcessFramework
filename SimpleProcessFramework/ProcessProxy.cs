using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework
{
    public interface IInterprocessRequestHandler
    {
        Task<object> ProcessCall(IClientInterprocessConnection targetConnection, RemoteCallRequest remoteCallRequest, CancellationToken ct);
    }

    public class ProcessProxy : IInterprocessRequestHandler
    {
        private readonly IClientConnectionFactory m_connectionFactory;
        private readonly ITypeResolver m_typeResolver;

        public ProcessProxy()
            : this(ProcessCluster.DefaultTypeResolver.CreateNewScope())
        {
        }

        public ProcessProxy(ITypeResolver resolver)
        {
            m_typeResolver = resolver.CreateNewScope();
            m_connectionFactory = m_typeResolver.CreateSingleton<IClientConnectionFactory>();
        }

        public T CreateInterface<T>(ProcessEndpointAddress address)
        {
            var proxy = ProcessProxyFactory.CreateImplementation<T>();
            proxy.Initialize(this, m_connectionFactory.GetConnection(address), address);
            return (T)(object)proxy;
        }

        async Task<object> IInterprocessRequestHandler.ProcessCall(IClientInterprocessConnection targetConnection, RemoteCallRequest req, CancellationToken ct)
        {
            var t = targetConnection.SerializeAndSendMessage(req);

            var ctRegistration = new CancellationTokenRegistration();

            if (ct.CanBeCanceled)
            {
                ct.Register(() => targetConnection.SerializeAndSendMessage(new RemoteCallCancellationRequest
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
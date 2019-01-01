using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework
{
    internal interface IInterprocessConnection : IDisposable
    {
        void Initialize();
        Task<object> SendRequest(IInterprocessRequest req);
    }


    internal enum DataKind : byte
    {
        Null = 0xAA,
        Graph = 0xBB,
        Type = 0xCC,
        Assembly = 0xDD
    }

    internal interface IBinarySerializer
    {
        Stream Serialize<T>(T graph);
        T Deserialize<T>(Stream s);
    }

    internal class SerializerSession
    {
        public Stream Stream { get; }
        public BinaryWriter Writer { get; }

        public SerializerSession(Stream ms)
        {
            Stream = ms;
            Writer = new BinaryWriter(ms);
        }

        internal void WriteType(Type actualType)
        {
            Stream.WriteByte((byte)DataKind.Type);
            Writer.Write(actualType.AssemblyQualifiedName);
        }
    }

    internal interface ITypeSerializer
    {
        void WriteObject(SerializerSession bw, object graph);
        object ReadObject(DeserializerSession reader);
    }

    internal class DeserializerSession
    {
        public Stream Stream { get; }
        public BinaryReader Reader { get; }

        public DeserializerSession(Stream s)
        {
            Stream = s;
            Reader = new BinaryReader(s);
        }
    }

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

            CancellationTokenRegistration ctRegistration = new CancellationTokenRegistration();

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
using SimpleProcessFramework;
using SimpleProcessFramework.CoreEndpoints;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();

/*            var aaa = (ProcessEndpointHandler)ProcessEndpointHandlerFactory.Create<IProcessManager>(new ProcessManager());

            var clientChannel = new InterprocessClientChannel();
            var clientContext = new InterprocessClientContext(clientChannel);

            aaa.HandleMessage(new InterprocessRequestContext(aaa, clientContext, new RemoteCallRequest
            {
                CallId =  3,
                MethodId = 2,
                Args = new object[] {5, cts.Token},
                Cancellable = true
            }));

            aaa.HandleMessage(new InterprocessRequestContext(aaa, clientContext, new RemoteCallCancellationRequest
            {
                CallId = 3
            }));

            Thread.Sleep(-1);
            */
            var processCluster = new ProcessCluster(new ProcessClusterConfiguration
            {

            });

            X509Store s = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            s.Open(OpenFlags.ReadWrite);
            var cert = s.Certificates.Cast<X509Certificate2>().FirstOrDefault(c => c.HasPrivateKey && c.GetRSAPrivateKey() != null);
            s.Close();

            var list = TcpListener.Create(ProcessCluster.DefaultRemotePort);
            list.Start();
            list.AcceptTcpClientAsync().ContinueWith(async t=>
            {
                var ns = t.Result.GetStream();
                var ssl = new SslStream(ns);
                ssl.AuthenticateAsServer(cert);

                var ser = new DefaultBinarySerializer();

                var msg = await ssl.ReadLengthPrefixedBlock();
                var sssj = ser.Deserialize<object>(msg);

                var ssss = ser.Serialize<object>(new RemoteClientConnectionResponse
                {
                    Success = true
                }, lengthPrefix: true);

                ssss.CopyTo(ssl);

                msg = await ssl.ReadLengthPrefixedBlock();
                sssj = ser.Deserialize<IInterprocessRequest>(msg);

                ssss = ser.Serialize<IInterprocessRequest>(new RemoteCallSuccessResponse
                {
                   CallId = 0,
                   Result = null
                }, lengthPrefix: true);

                ssss.CopyTo(ssl);

                ssl.Flush();
            }).Unwrap();

            cts = new CancellationTokenSource();
            var proc = processCluster.PrimaryProxy.CreateInterface<IProcessManager>(new ProcessEndpointAddress("localhost", "master"));
            proc.AutoDestroy2(new ZOOM(), default).Wait();

            Thread.Sleep(-1);

            Console.WriteLine("Hello World!");
        }
    }
}
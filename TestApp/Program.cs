using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Runtime.Server;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

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

            processCluster.AddListener(new TlsInterprocessConnectionListener(cert, ProcessCluster.DefaultRemotePort));

            cts = new CancellationTokenSource();
            var proc = processCluster.PrimaryProxy.CreateInterface<IProcessManager>(new ProcessEndpointAddress("localhost", "master", "ProcessManager"));
            proc.CreateProcess(new ProcessCreationInfo
            {
                ProcessName = "Test"
            }, mustCreate: true).Wait();

            Thread.Sleep(-1);

            Console.WriteLine("Hello World!");
        }
    }
}
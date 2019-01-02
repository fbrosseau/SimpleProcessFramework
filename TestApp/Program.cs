using SimpleProcessFramework;
using SimpleProcessFramework.CoreEndpoints;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server;
using System;
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

            cts = new CancellationTokenSource();
            var proc = processCluster.PrimaryProxy.CreateInterface<IProcessManager>(processCluster.MasterProcess.CreateRelativeAddress());
            proc.AutoDestroy2(new ZOOM(), default).Wait();

            Console.WriteLine("Hello World!");
        }
    }
}
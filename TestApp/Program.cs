using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Tests.Integration;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.TestApp
{
    public interface ITest
    {
        event EventHandler Allo;
        Task LOL();
    }

    public class GreatTest : AbstractProcessEndpoint, ITest
    {
        public event EventHandler Allo;

        protected override Task InitializeAsync()
        {
            return base.InitializeAsync();
        }

        public Task LOL()
        {
            Allo?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var t = new GeneralEndToEndSanity();
            t.Init();
            t.BasicDefaultNameSubprocess();
            t.Cleanup();

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
            var proc = processCluster.PrimaryProxy.CreateInterface<IProcessBroker>(ProcessEndpointAddress.Parse($"/master/{WellKnownEndpoints.ProcessBroker}"));
           /* proc.CreateProcess(new ProcessCreationInfo
            {
                ProcessName = "",
                ProcessKind = ProcessKind.Netfx,
                ProcessUniqueId = "Test"
            }, mustCreate: true).Wait();
            */

            var testPRocess = "master";

            var remoteProcess = processCluster.PrimaryProxy.CreateInterface<IEndpointBroker>(new ProcessEndpointAddress("localhost", testPRocess, WellKnownEndpoints.EndpointBroker));
            var res = remoteProcess.CreateEndpoint("LOL", ReflectedTypeInfo.Create(typeof(ITest)), ReflectedTypeInfo.Create(typeof(GreatTest))).Result;

            //var wowow = remoteProcess.GetProcessCreationInfo().Result;

            var remoteTest = processCluster.PrimaryProxy.CreateInterface<ITest>(new ProcessEndpointAddress("localhost", testPRocess, "LOL"));
            remoteTest.Allo += RemoteTest_Allo;
            remoteTest.LOL().Wait();

            Thread.Sleep(-1);

            Console.WriteLine("Hello World!");
        }

        private static void RemoteTest_Allo(object sender, EventArgs e)
        {
        }
    }
}
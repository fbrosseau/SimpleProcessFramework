using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Tests.Integration;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

    public class UnixEndpoint : EndPoint
    {
        private static readonly UTF8Encoding s_rawUtf8 = new UTF8Encoding(false, false);

        public static UnixEndpoint Empty { get; } = new UnixEndpoint("");
        public string Address { get; }
        public override AddressFamily AddressFamily => AddressFamily.Unix;

        public UnixEndpoint(string addr)
        {
            Address = addr;
        }

        public override SocketAddress Serialize()
        {
            var addr = new SocketAddress(AddressFamily.Unix, 300);

            var bytes = s_rawUtf8.GetBytes(Address);
            for (int i = 0; i < bytes.Length; ++i)
            {
                addr[2 + i] = bytes[i];
            }

            return addr;
        }

        public override EndPoint Create(SocketAddress socketAddress)
        {
            int firstNull = 2;
            while (firstNull < socketAddress.Size && socketAddress[firstNull] != 0)
                ++firstNull;

            if (firstNull == 2)
                return Empty;

            byte[] buf = new byte[firstNull - 2];
            for (int i = 2; i < firstNull; ++i)
                buf[i - 2] = socketAddress[i];

            return new UnixEndpoint(s_rawUtf8.GetString(buf));
        }

        public override string ToString()
        {
            return "unix:" + Address;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
#if no
            var wiwi = new ProcessStartInfo(@"""C:\Windows\system32\wsl.exe"" ""dotnet"" ""/mnt/c/Users/fb/source/repos/SimpleProcessFramework/bin/debug/Spfx.Process.Netcore.dll"" ""dc43ad486f154d1baf48277a6f1edf6a"" ""15796""")
            {
                //           RedirectStandardOutput = true,
                //             RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var profc = Process.Start(wiwi);

       /*     profc.OutputDataReceived += (sender, e) =>
            {

            };

            profc.ErrorDataReceived += (sender, e) =>
            {

            };

            profc.BeginErrorReadLine();
            profc.BeginOutputReadLine();
            */
            profc.StandardInput.WriteLine("ALLO");

            profc.WaitForExit();
            Thread.Sleep(-1);

            var addr = "\0C:\\Users\\fb\\patate.txt";
            var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            sock.Bind(new UnixEndpoint(addr));
            sock.Listen(5);
            var cli = sock.Accept();
            var agc = cli.Receive(new byte[342]);
#endif

            var t = new GeneralEndToEndSanity();
            t.Init();
            t.BasicDefaultNameSubprocess_Wsl();
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
using System;
using Spfx;
using Spfx.Interfaces;
using Spfx.Runtime.Server;
using System.Threading.Tasks;
using Spfx.Utilities;

namespace TestApp234
{

    public interface IZigZag
    {
        Task<string> Test();
    }

    public class ZigZag : AbstractProcessEndpoint, IZigZag
    {
        public Task<string> Test()
        {
            return Task.FromResult("Allo");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine(string.Join(", ", HostFeaturesHelper.GetInstalledNetcoreRuntimes()));
            System.Console.WriteLine(HostFeaturesHelper.IsWindows);
            System.Console.WriteLine(System.Diagnostics.Process.GetCurrentProcess().Id);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("\"/usr/bin/whereis\" \"dotnet\"")
            {
                UseShellExecute = false
            }).WaitForExit();

            Console.ReadLine();

            HostFeaturesHelper.GetInstalledNetcoreRuntimes();

            var c = new ProcessCluster();
            c.MasterProcess.ProcessBroker.CreateProcessAndEndpoint(new ProcessCreationRequest
            {
                Options = ProcessCreationOptions.ThrowIfExists,
                ProcessInfo = new ProcessCreationInfo
                {
                    ProcessKind = ProcessKind.Netfx32,
                    ProcessUniqueId = "LOL"
                }
            }, new EndpointCreationRequest
            {
                EndpointId = "LOL",
                EndpointType = typeof(IZigZag),
                ImplementationType = typeof(ZigZag)
            }).Wait();

            var iii = c.PrimaryProxy.CreateInterface<IZigZag>("/LOL/LOL");
            var yoyo = iii.Test().Result;


            Console.ReadLine();
        }
    }
}

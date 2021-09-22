using Spfx;
using Spfx.Interfaces;
using Spfx.Runtime.Server;
using System;
using System.Threading.Tasks;

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
            var c = new ProcessCluster();
            c.MasterProcess.ProcessBroker.CreateProcessAndEndpoint(new ProcessCreationRequest
            {
                Options = ProcessCreationOptions.ThrowIfExists,
                ProcessInfo = new ProcessCreationInfo
                {
                    TargetFramework = TargetFramework.Create(ProcessKind.Netfx32),
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

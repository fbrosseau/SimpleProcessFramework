using Spfx.Reflection;
using Spfx.Utilities.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Interfaces
{
    public class ProcessClusterHostInformation
    {
        [DataMember]
        public string OSDescription { get; private set; }
        [DataMember]
        public string FrameworkDescription { get; private set; }
        [DataMember]
        public string MachineName { get; private set; }
        [DataMember]
        public string DnsName { get; private set; }
        [DataMember]
        public OsKind OSKind { get; private set; }
        [DataMember]
        public int CoreCount { get; private set; }
        [DataMember]
        public string[] AvailableNetcoreRuntimes { get; private set; }

        private static ProcessClusterHostInformation s_current;
        public static ProcessClusterHostInformation GetCurrent()
        {
            if (s_current != null)
                return s_current;

            s_current = new ProcessClusterHostInformation
            {
                MachineName = Environment.MachineName,
                DnsName = Dns.GetHostEntry("").HostName,
                CoreCount = Environment.ProcessorCount,
                OSDescription = RuntimeInformation.OSDescription,
                OSKind = HostFeaturesHelper.LocalMachineOsKind,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                AvailableNetcoreRuntimes = NetcoreInfo.Default.InstalledVersions.ToArray()
            };

            return s_current;
        }
    }

    public enum ProcessCreationResults
    {
        CreatedNew,
        AlreadyExists,
        Failure
    }

    [Flags]
    public enum ProcessCreationOptions
    {
        ContinueIfExists = 1,
        ThrowIfExists = 2
    }

    [DataContract]
    public class EndpointCreationRequest
    {
        [DataMember]
        public string EndpointId { get; set; }
        [DataMember]
        public ReflectedTypeInfo EndpointType { get; set; }
        [DataMember]
        public ReflectedTypeInfo ImplementationType { get; set; }
        [DataMember]
        public ProcessCreationOptions Options { get; set; } = ProcessCreationOptions.ThrowIfExists;

        internal void EnsureIsValid()
        {
            //throw new NotImplementedException();
        }
    }

    [DataContract]
    public class ProcessEventArgs : EventArgs
    {
        [DataMember]
        public string ProcessUniqueId { get; }

        public ProcessEventArgs(string uniqueId)
        {
            ProcessUniqueId = uniqueId;
        }
    }

    [DataContract]
    public class ProcessInformation
    {
        [DataMember]
        public string ProcessName { get; }

        [DataMember]
        public int OsPid { get; }

        [DataMember]
        public TargetFramework Framework { get; }

        public ProcessInformation(string name, int pid, TargetFramework fx)
        {
            ProcessName = name;
            OsPid = pid;
            Framework = fx;
        }
    }

    [DataContract]
    public class ProcessAndEndpointCreationOutcome
    {
        [DataMember]
        public ProcessCreationResults ProcessOutcome { get; }
        [DataMember]
        public ProcessCreationResults EndpointOutcome { get; }

        public ProcessAndEndpointCreationOutcome(ProcessCreationResults processOutcome, ProcessCreationResults endpointOutcome)
        {
            ProcessOutcome = processOutcome;
            EndpointOutcome = endpointOutcome;
        }
    }

    public interface IProcessBroker
    {
        event EventHandler<ProcessEventArgs> ProcessCreated;
        event EventHandler<ProcessEventArgs> ProcessLost;

        Task<List<ProcessInformation>> GetAllProcesses();
        Task<ProcessInformation> GetProcessInformation(string processName);

        Task<ProcessClusterHostInformation> GetHostInformation();

        Task<ProcessCreationResults> CreateProcess(ProcessCreationRequest req);
        Task<ProcessAndEndpointCreationOutcome> CreateProcessAndEndpoint(ProcessCreationRequest processInfo, EndpointCreationRequest endpointInfo);

        Task<bool> DestroyProcess(string processName, bool onlyIfEmpty = true);
    }
}
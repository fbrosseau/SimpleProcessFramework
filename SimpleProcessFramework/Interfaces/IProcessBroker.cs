using Spfx.Reflection;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
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
                AvailableNetcoreRuntimes = HostFeaturesHelper.GetInstalledNetcoreRuntimes()
            };

            return s_current;
        }
    }

    public enum ProcessCreationOutcome
    {
        CreatedNew,
        AlreadyExists,
        Failure
    }

    public enum ProcessCreationOptions
    {
        ContinueIfExists,
        ThrowIfExists
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
        public ProcessKind ProcessKind { get; }

        public ProcessInformation(string name, int pid, ProcessKind kind)
        {
            ProcessName = name;
            OsPid = pid;
            ProcessKind = kind;
        }
    }

    [DataContract]
    public class ProcessAndEndpointCreationOutcome
    {
        [DataMember]
        public ProcessCreationOutcome ProcessOutcome { get; }
        [DataMember]
        public ProcessCreationOutcome EndpointOutcome { get; }

        public ProcessAndEndpointCreationOutcome(ProcessCreationOutcome processOutcome, ProcessCreationOutcome endpointOutcome)
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

        Task<ProcessCreationOutcome> CreateProcess(ProcessCreationRequest req);
        Task<ProcessAndEndpointCreationOutcome> CreateProcessAndEndpoint(ProcessCreationRequest processInfo, EndpointCreationRequest endpointInfo);

        Task<bool> DestroyProcess(string processName, bool onlyIfEmpty = true);
    }
}
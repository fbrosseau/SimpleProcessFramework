using Spfx.Reflection;
using System;
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

        private static ProcessClusterHostInformation s_current;
        public static ProcessClusterHostInformation GetCurrent()
        {
            if (s_current != null)
                return s_current;

            OsKind osKind;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                osKind = OsKind.Windows;
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                osKind = OsKind.Linux;
            }
            else
            {
                osKind = OsKind.Other;
            }

            s_current = new ProcessClusterHostInformation
            {
                MachineName = Environment.MachineName,
                DnsName = Dns.GetHostEntry("").HostName,
                CoreCount = Environment.ProcessorCount,
                OSDescription = RuntimeInformation.OSDescription,
                OSKind = osKind,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
            };

            return s_current;
        }
    }

    public enum ProcessCreationOutcome
    {
        CreatedNew,
        ProcessAlreadyExists,
        EndpointAlreadyExists
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
        public bool FailIfExists { get; set; } = true;

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

    public interface IProcessBroker
    {
        event EventHandler<ProcessEventArgs> ProcessCreated;
        event EventHandler<ProcessEventArgs> ProcessLost;

        Task<ProcessClusterHostInformation> GetHostInformation();

        Task<ProcessCreationOutcome> CreateProcess(ProcessCreationRequest req);
        Task<ProcessCreationOutcome> CreateEndpoint(ProcessCreationRequest processInfo, EndpointCreationRequest endpointInfo);

        Task<bool> DestroyProcess(string processName, bool onlyIfEmpty = true);
    }
}
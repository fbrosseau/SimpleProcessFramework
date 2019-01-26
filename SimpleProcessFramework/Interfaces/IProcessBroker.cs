using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
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

            s_current = new ProcessClusterHostInformation
            {
                MachineName = Environment.MachineName,
                DnsName = Dns.GetHostEntry("").HostName,
                CoreCount = Environment.ProcessorCount,
                OSDescription = RuntimeInformation.OSDescription,
#if WINDOWS_BUILD
                OSKind = OsKind.Windows,
#else
                OSKind = OsKind.Other,
#endif
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
            };

            return s_current;
        }
    }

    public interface IProcessBroker
    {
        Task<ProcessClusterHostInformation> GetHostInformation();

        Task<bool> CreateProcess(ProcessCreationRequest req);
        Task<bool> DestroyProcess(string processName);
    }
}

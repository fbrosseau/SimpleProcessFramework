using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
{
    public enum ProcessKind
    {
        Default,
        Netfx,
        Netfx32,
        Netcore,
        Netcore32,
    }

    [DataContract]
    public class ProcessCreationInfo
    {
        [DataMember]
        public ProcessKind ProcessKind { get; set; }

        [DataMember]
        public string ProcessName { get; set; }
    }

    public interface IProcessManager
    {
        Task<bool> CreateProcess(ProcessCreationInfo info, bool mustCreate);
        Task<bool> DestroyProcess(string processName);
    }
}

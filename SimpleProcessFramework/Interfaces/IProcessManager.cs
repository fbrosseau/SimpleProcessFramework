using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
{
    [DataContract]
    public class ProcessCreationInfo
    {
        [DataMember]
        public string ProcessName { get; set; }
    }

    public interface IProcessManager
    {
        Task<bool> CreateProcess(ProcessCreationInfo info, bool mustCreate);
        Task<bool> DestroyProcess(string processName);
    }
}

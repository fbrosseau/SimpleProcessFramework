using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
{
    public interface IProcessBroker
    {
        Task<bool> CreateProcess(ProcessCreationInfo info, bool mustCreate);
        Task<bool> DestroyProcess(string processName);
    }
}

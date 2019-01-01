using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientChannel
    {
        void SendResponse(long callId, Task<object> completion);
    }
}
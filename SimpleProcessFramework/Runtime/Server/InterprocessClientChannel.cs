using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class InterprocessClientChannel : IInterprocessClientChannel
    {
        public void SendResponse(long callId, Task<object> completion)
        {
            throw new System.NotImplementedException();
        }
    }
}
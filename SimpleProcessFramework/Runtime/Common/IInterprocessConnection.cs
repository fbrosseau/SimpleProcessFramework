using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Common
{
    public interface IInterprocessConnection : IDisposable
    {
        void Initialize();
        Task<object> SerializeAndSendMessage(IInterprocessMessage req);
    }
}
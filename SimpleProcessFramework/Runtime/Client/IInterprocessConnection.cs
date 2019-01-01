using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    internal interface IInterprocessConnection : IDisposable
    {
        void Initialize();
        Task<object> SendRequest(IInterprocessRequest req);
    }
}
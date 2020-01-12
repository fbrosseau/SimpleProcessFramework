using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    internal interface ICallbackInterface
    {
        Task<int> Double(int i);
    }

    internal class CallbackInterface : ICallbackInterface
    {
        public Task<int> Double(int i)
        {
            return Task.FromResult(i * 2);
        }
    }
}
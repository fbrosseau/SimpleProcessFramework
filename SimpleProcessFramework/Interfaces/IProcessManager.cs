using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
{
    public interface IProcessManager
    {
        Task AutoDestroy();
        Task AutoDestroy3();
        Task AutoDestroy2(int i, CancellationToken ct);
    }
}

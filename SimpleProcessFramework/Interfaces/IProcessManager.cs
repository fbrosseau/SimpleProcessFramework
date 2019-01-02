using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Interfaces
{
    [DataContract]
    public class ZOOM
    {

    }

    public interface IProcessManager
    {
        Task AutoDestroy();
        Task AutoDestroy3();
        Task AutoDestroy2(ZOOM i, CancellationToken ct);
    }
}

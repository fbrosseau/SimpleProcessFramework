using System;
using System.Threading.Tasks;

namespace Spfx.Io
{
    public interface ILengthPrefixedStreamReader : IDisposable
    {
        ValueTask<StreamOrCode> GetNextFrame();
    }
}

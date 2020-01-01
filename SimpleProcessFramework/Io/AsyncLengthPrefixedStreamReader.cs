using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal sealed class AsyncLengthPrefixedStreamReader : BaseLengthPrefixedStreamReader
    {
        public AsyncLengthPrefixedStreamReader(Stream stream, int maximumFrameSize = int.MaxValue)
            : base(stream, maximumFrameSize)
        {
        }

        internal override ValueTask<StreamOrCode> ReceiveNextFrame()
        {
            return Stream.ReadCodeOrLengthPrefixedBlockAsync(MaximumFrameSize, sizeBuffer: SizeBuffer);
        }
    }
}

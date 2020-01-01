using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal class SyncLengthPrefixedStreamReader : BaseLengthPrefixedStreamReader
    {
        private readonly string m_name;

        public SyncLengthPrefixedStreamReader(Stream stream, string name, int maximumFrameSize = int.MaxValue)
            : base(stream, maximumFrameSize)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_name = name;
        }

        protected override async Task InitializeAsync()
        {
            await TaskEx.SwitchToNewThread(m_name);
        }

        internal override ValueTask<StreamOrCode> ReceiveNextFrame()
        {
            return new ValueTask<StreamOrCode>(Stream.ReadCodeOrLengthPrefixedBlock(MaximumFrameSize, sizeBuffer: SizeBuffer));
        }
    }
}

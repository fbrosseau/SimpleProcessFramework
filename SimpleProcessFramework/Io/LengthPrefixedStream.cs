using Spfx.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    public struct LengthPrefixedStream : IDisposable
    {
        private readonly Stream m_stream;
        public Stream Stream
        {
            get
            {
                if (m_stream is null)
                    throw new InvalidOperationException("This frame had no data");
                return m_stream;
            }
        }

        public int StreamLength { get; }

        public LengthPrefixedStream(int streamLength, Stream stream = null)
        {
            m_stream = stream;
            StreamLength = streamLength;
        }

        public LengthPrefixedStream(Stream stream)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;
            StreamLength = checked((int)(stream.Length - 4));
        }

        public void Dispose()
        {
            m_stream?.Dispose();
        }

        internal static ILengthPrefixedStreamWriter CreateWriter(Stream stream, string name, bool? sync = null)
        {
            if (sync == null)
                sync = !HostFeaturesHelper.IsWindows;

            if (sync == true)
                return new SyncLengthPrefixedStreamWriter(stream, name);

            return new AsyncLengthPrefixedStreamWriter(stream);
        }

        internal static ILengthPrefixedStreamReader CreateReader(Stream stream, string name, bool? sync = null)
        {
            if (sync == null)
                sync = !HostFeaturesHelper.IsWindows;

            if (sync == true)
                return new SyncLengthPrefixedStreamReader(stream, name);

            return new AsyncLengthPrefixedStreamReader(stream);
        }
    }

    public interface ILengthPrefixedStreamWriter : IDisposable
    {
        void WriteFrame(LengthPrefixedStream frame);
    }

    public interface ILengthPrefixedStreamReader : IDisposable
    {
        ValueTask<LengthPrefixedStream> GetNextFrame();
    }
}

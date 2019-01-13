using Oopi.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Io
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

using Spfx.Utilities;
using System;
using System.IO;
using System.IO.Pipes;
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
                sync = UseStreamSynchronously(stream);

            if (sync == true)
                return new SyncLengthPrefixedStreamWriter(stream, name);

            return new AsyncLengthPrefixedStreamWriter(stream);
        }

        internal static ILengthPrefixedStreamReader CreateReader(Stream stream, string name, bool? sync = null)
        {
            if (sync == null)
                sync = UseStreamSynchronously(stream);

            if (sync == true)
                return new SyncLengthPrefixedStreamReader(stream, name);

            return new AsyncLengthPrefixedStreamReader(stream);
        }

        private static bool UseStreamSynchronously(Stream stream)
        {
            if (!HostFeaturesHelper.IsWindows)
                return true;

            if (stream is PipeStream)
            {
                if (stream is AnonymousPipeServerStream || stream is AnonymousPipeClientStream)
                    return true;
            }

            return false;
        }
    }

    public class StreamWriterExceptionEventArgs : EventArgs
    {
        public Exception CaughtException { get; }

        public StreamWriterExceptionEventArgs(Exception ex)
        {
            CaughtException = ex;
        }
    }

    public interface ILengthPrefixedStreamWriter : IDisposable
    {
        event EventHandler<StreamWriterExceptionEventArgs> WriteException;
        void WriteFrame(LengthPrefixedStream frame);
    }

    public interface ILengthPrefixedStreamReader : IDisposable
    {
        ValueTask<LengthPrefixedStream> GetNextFrame();
    }
}

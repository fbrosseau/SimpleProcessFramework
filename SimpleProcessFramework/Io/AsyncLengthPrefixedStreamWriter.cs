using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal class AsyncLengthPrefixedStreamWriter : ILengthPrefixedStreamWriter
    {
        private readonly Stream m_stream;
        private readonly Task m_writeThread;
        private readonly AsyncQueue<LengthPrefixedStream> m_pendingWrites;
        private readonly byte[] m_temp = new byte[4];

        public AsyncLengthPrefixedStreamWriter(Stream stream)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_pendingWrites = new AsyncQueue<LengthPrefixedStream>
            {
                DisposeIgnoredItems = true
            };

            m_writeThread = Task.Run(WriteLoop);
        }

        public void Dispose()
        {
            m_writeThread.FireAndForget(); // only for debugging
            m_pendingWrites.Dispose();
            m_stream.Dispose();
        }

        public void WriteFrame(LengthPrefixedStream frame)
        {
            m_pendingWrites.Enqueue(frame);
        }

        private async Task WriteLoop()
        {
            try
            {
                await m_pendingWrites.ForEachAsync(s => DoWrite(s));
            }
            catch (Exception ex)
            {
                m_pendingWrites.Dispose(ex);
            }
        }

        private async ValueTask DoWrite(LengthPrefixedStream frame)
        {
            using (frame)
            {
                if (frame.StreamLength <= 0)
                {
                    m_temp[0] = (byte)frame.StreamLength;
                    m_temp[1] = (byte)(frame.StreamLength >> 8);
                    m_temp[2] = (byte)(frame.StreamLength >> 16);
                    m_temp[3] = (byte)(frame.StreamLength >> 24);
                    await m_stream.WriteAsync(m_temp, 0, 4);
                }
                else
                {
                    await frame.Stream.CopyToAsync(m_stream);
                }
            }
        }
    }
}
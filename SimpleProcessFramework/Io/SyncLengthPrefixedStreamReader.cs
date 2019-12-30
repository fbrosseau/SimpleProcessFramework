using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal class SyncLengthPrefixedStreamReader : ILengthPrefixedStreamReader
    {
        private readonly Stream m_stream;
        private readonly Thread m_readThread;
        private readonly AsyncQueue<ReceivedFrame> m_readQueue;

        public SyncLengthPrefixedStreamReader(Stream stream, string name)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_readQueue = new AsyncQueue<ReceivedFrame>
            {
                DisposeIgnoredItems = true
            };

            m_readThread = new Thread(ReadLoop)
            {
                Name = name,
                IsBackground = true
            };

            m_readThread.Start();
        }

        public void Dispose()
        {
            m_readQueue.Dispose();
            m_stream.Dispose();
        }

        private void ReadLoop()
        {
            try
            {
                byte[] sizeBuffer = new byte[4];
                while (true)
                {
                    m_stream.ReadAllBytes(new ArraySegment<byte>(sizeBuffer, 0, 4));
                    int count = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(sizeBuffer, 0, 4));
                    if (count <= 0)
                    {
                        m_readQueue.Enqueue(ReceivedFrame.CreateCodeFrame(count));
                        continue;
                    }

                    byte[] buf = ArrayPool<byte>.Shared.Rent(count);
                    try
                    {
                        m_stream.ReadAllBytes(new ArraySegment<byte>(buf, 0, count));
                    }
                    catch
                    {
                        ArrayPool<byte>.Shared.Return(buf);
                        throw;
                    }

                    m_readQueue.Enqueue(ReceivedFrame.CreateFromRentedArray(buf, count));
                }
            }
            catch (Exception ex)
            {
                m_readQueue.Dispose(ex);
            }
        }

        public ValueTask<ReceivedFrame> GetNextFrame()
        {
            return m_readQueue.Dequeue();
        }
    }
}

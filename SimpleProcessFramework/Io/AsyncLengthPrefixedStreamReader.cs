using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal class AsyncLengthPrefixedStreamReader : ILengthPrefixedStreamReader
    {
        private readonly Stream m_stream;
        private readonly Task m_readThread;
        private readonly AsyncQueue<ReceivedFrame> m_readQueue;

        public AsyncLengthPrefixedStreamReader(Stream stream)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_readQueue = new AsyncQueue<ReceivedFrame>
            {
                DisposeIgnoredItems = true
            };

            m_readThread = Task.Run(ReadLoop);
        }

        public void Dispose()
        {
            m_stream.Dispose();
            m_readQueue.Dispose();
            m_readThread.FireAndForget(); // only for debugging
        }

        private async Task ReadLoop()
        {
            try
            {
                var sizeBuffer = new byte[4];
                while (true)
                {
                    await m_stream.ReadAllBytesAsync(new ArraySegment<byte>(sizeBuffer, 0, 4)).ConfigureAwait(false);
                    int count = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(sizeBuffer, 0, 4));
                    if (count <= 0)
                    {
                        m_readQueue.Enqueue(ReceivedFrame.CreateCodeFrame(count));
                        continue;
                    }

                    byte[] buf = ArrayPool<byte>.Shared.Rent(count);
                    try
                    {
                        await m_stream.ReadAllBytesAsync(new ArraySegment<byte>(buf, 0, count)).ConfigureAwait(false);
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

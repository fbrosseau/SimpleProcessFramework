using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal class SyncLengthPrefixedStreamReader : ILengthPrefixedStreamReader
    {
        private readonly Stream m_stream;
        private readonly Thread m_readThread;
        private readonly AsyncQueue<LengthPrefixedStream> m_readQueue;

        public SyncLengthPrefixedStreamReader(Stream stream, string name)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_readQueue = new AsyncQueue<LengthPrefixedStream>
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
                byte[] sizeBuffer = null;
                while (true)
                {
                    if (sizeBuffer is null)
                        sizeBuffer = new byte[4];

                    m_stream.ReadAllBytes(new ArraySegment<byte>(sizeBuffer, 0, 4));
                    int count = BitConverter.ToInt32(sizeBuffer, 0);
                    if (count <= 0)
                    {
                        m_readQueue.Enqueue(new LengthPrefixedStream(count));
                        continue;
                    }

                    byte[] buf;
                    if(count <= 4)
                    {
                        buf = sizeBuffer;
                        sizeBuffer = null;
                    }
                    else
                    {
                        buf = new byte[count];
                    }

                    m_stream.ReadAllBytes(new ArraySegment<byte>(buf, 0, count));
                    m_readQueue.Enqueue(new LengthPrefixedStream(count, new MemoryStream(buf, 0, count)));
                }
            }
            catch (Exception ex)
            {
                m_readQueue.Dispose(ex);
            }
        }

        public ValueTask<LengthPrefixedStream> GetNextFrame()
        {
            return m_readQueue.Dequeue();
        }
    }
}

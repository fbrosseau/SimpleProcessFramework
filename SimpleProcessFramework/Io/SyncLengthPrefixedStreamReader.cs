using Oopi.Utilities;
using SimpleProcessFramework.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Io
{
    internal class SyncLengthPrefixedStreamReader : ILengthPrefixedStreamReader
    {
        private readonly Stream m_stream;
        private readonly Thread m_readThread;
        private readonly AsyncQueue<LengthPrefixedStream> m_readQueue = new AsyncQueue<LengthPrefixedStream>();

        public SyncLengthPrefixedStreamReader(Stream stream, string name)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_readThread = new Thread(ReadLoop)
            {
                Name = name
            };

            m_readThread.Start();
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

                    m_stream.ReadBytes(new ArraySegment<byte>(sizeBuffer, 0, 4));
                    int count = BitConverter.ToInt32(sizeBuffer, 0);
                    if (count <= 0)
                    {
                        m_readQueue.Enqueue(new LengthPrefixedStream(count));
                        continue;
                    }

                    byte[] buf;
                    if(count < 4)
                    {
                        buf = sizeBuffer;
                        sizeBuffer = null;
                    }
                    else
                    {
                        buf = new byte[count];
                    }

                    m_stream.ReadBytes(new ArraySegment<byte>(buf, 0, count));
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

        public void Dispose()
        {
            m_stream.Dispose();
        }
    }
}

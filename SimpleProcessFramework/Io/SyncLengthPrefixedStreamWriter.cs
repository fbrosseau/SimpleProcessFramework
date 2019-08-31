using Spfx.Utilities;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Spfx.Io
{
    internal class SyncLengthPrefixedStreamWriter : ILengthPrefixedStreamWriter
    {
        private readonly Stream m_stream;
        private readonly Thread m_writeThread;
        private readonly BlockingCollection<LengthPrefixedStream> m_pendingWrites = new BlockingCollection<LengthPrefixedStream>();

        public event EventHandler<StreamWriterExceptionEventArgs> WriteException;

        public SyncLengthPrefixedStreamWriter(Stream stream, string name)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_writeThread = new Thread(WriteLoop)
            {
                Name = name,
                IsBackground = true
            };

            m_writeThread.Start();
        }

        private void WriteLoop()
        {
            try
            {
                var tempBuffer = new byte[4];
                foreach (var frame in m_pendingWrites.GetConsumingEnumerable())
                {
                    using (frame)
                    {
                        if (frame.StreamLength <= 0)
                        {
                            tempBuffer[0] = (byte)frame.StreamLength;
                            tempBuffer[1] = (byte)(frame.StreamLength >> 8);
                            tempBuffer[2] = (byte)(frame.StreamLength >> 16);
                            tempBuffer[3] = (byte)(frame.StreamLength >> 24);
                            m_stream.Write(tempBuffer, 0, 4);
                        }
                        else
                        {
                            frame.Stream.CopyTo(m_stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteException?.Invoke(this, new StreamWriterExceptionEventArgs(ex));
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            m_pendingWrites.Dispose();
            m_stream.Dispose();
        }

        public void WriteFrame(LengthPrefixedStream frame)
        {
            m_pendingWrites.TryAdd(frame);
        }
    }
}

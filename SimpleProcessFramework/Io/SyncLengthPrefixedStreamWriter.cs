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
                foreach (var frame in m_pendingWrites.GetConsumingEnumerable())
                {
                    using (frame)
                    {
                        if (frame.StreamLength <= 0)
                        {
                            m_stream.WriteByte((byte)frame.StreamLength);
                            m_stream.WriteByte((byte)(frame.StreamLength >> 8));
                            m_stream.WriteByte((byte)(frame.StreamLength >> 16));
                            m_stream.WriteByte((byte)(frame.StreamLength >> 24));
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

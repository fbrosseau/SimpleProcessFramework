using Spfx.Diagnostics;
using Spfx.Utilities;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Spfx.Io
{
    internal class SyncLengthPrefixedStreamWriter : ILengthPrefixedStreamWriter
    {
        private readonly Stream m_stream;
        private readonly Thread m_writeThread;
        private readonly BlockingCollection<PendingWriteFrame> m_pendingWrites = new BlockingCollection<PendingWriteFrame>();

        public event EventHandler<StreamWriterExceptionEventArgs> WriteException;

        public SyncLengthPrefixedStreamWriter(Stream stream, string name)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            m_stream = stream;

            m_writeThread = CriticalTryCatch.StartThread(name, WriteLoop);
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
                        if (frame.IsCodeFrame)
                        {
                            int code = frame.Code;
                            BinaryPrimitives.WriteInt32LittleEndian(tempBuffer, code);
                            m_stream.Write(tempBuffer, 0, 4);
                        }
                        else
                        {
                            frame.DataStream.CopyTo(m_stream);
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

        public void WriteFrame(PendingWriteFrame frame)
        {
            m_pendingWrites.TryAdd(frame);
        }
    }
}

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

        public event EventHandler<StreamWriterExceptionEventArgs> WriteException;

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
                var tempBuffer = new byte[4];
                async ValueTask DoWrite(LengthPrefixedStream frame)
                {
                    using (frame)
                    {
                        if (frame.StreamLength <= 0)
                        {
                            tempBuffer[0] = (byte)frame.StreamLength;
                            tempBuffer[1] = (byte)(frame.StreamLength >> 8);
                            tempBuffer[2] = (byte)(frame.StreamLength >> 16);
                            tempBuffer[3] = (byte)(frame.StreamLength >> 24);
                            await m_stream.WriteAsync(tempBuffer, 0, 4).ConfigureAwait(false);
                        }
                        else
                        {
                            await frame.Stream.CopyToAsync(m_stream).ConfigureAwait(false);
                        }
                    }
                }

                await m_pendingWrites.ForEachAsync(s => DoWrite(s));
            }
            catch (Exception ex)
            {
                WriteException?.Invoke(this, new StreamWriterExceptionEventArgs(ex));
                m_pendingWrites.Dispose(ex);
            }
            finally
            {
                Dispose();
            }
        }
    }
}
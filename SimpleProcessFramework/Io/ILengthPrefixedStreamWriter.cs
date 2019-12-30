using System;

namespace Spfx.Io
{
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
        void WriteFrame(PendingWriteFrame frame);
    }
}

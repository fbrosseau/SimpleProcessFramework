using Spfx.Utilities;
using System;
using System.IO;

namespace Spfx.Io
{
    public struct PendingWriteFrame : IDisposable
    {
        private Stream m_stream;

        public bool IsCodeFrame => m_stream is null;
        public int Code { get; private set; }
        public Stream DataStream
        {
            get
            {
                if (IsCodeFrame)
                    BadCodeAssert.ThrowInvalidOperation("This frame does not contain data");
                return m_stream;
            }
        }

        internal static PendingWriteFrame CreateCodeFrame(int code)
        {
            return new PendingWriteFrame
            {
                Code = code
            };
        }

        internal static PendingWriteFrame CreateFromFramedData(Stream stream)
        {
            return new PendingWriteFrame
            {
                m_stream = stream
            };
        }

        public void Dispose()
        {
            if (m_stream != null)
            {
                m_stream.Dispose();
                m_stream = null;
            }
        }
    }
}

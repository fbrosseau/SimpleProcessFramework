using Spfx.Utilities;
using System;
using System.IO;

namespace Spfx.Io
{
    public struct ReceivedFrame : IDisposable
    {
        private const int UndefinedCode = 0;

        public bool IsCodeFrame => m_code != UndefinedCode;

        public int Code
        {
            get
            {
                if (!IsCodeFrame)
                    throw new InvalidOperationException("This is not a Code frame");
                return m_code;
            }
        }

        private int m_code;
        private Stream m_stream;

        public static ReceivedFrame CreateCodeFrame(int code)
        {
            return new ReceivedFrame
            {
                m_code = code
            };
        }

        public static ReceivedFrame CreateFromData(Stream data)
        {
            return new ReceivedFrame
            {
                m_stream = data,
                m_code = UndefinedCode
            };
        }

        public static ReceivedFrame CreateFromRentedArray(byte[] bytes, int count)
        {
            return CreateFromData(RentedMemoryStream.CreateFromRentedArray(bytes, count, false));
        }

        public Stream AcquireDataStream()
        {
            var s = m_stream;
            m_stream = null;
            if (s is null)
                throw new InvalidOperationException("This frame has no data");
            return s;
        }

        public void Dispose()
        {
            m_stream?.Dispose();
        }
    }
}

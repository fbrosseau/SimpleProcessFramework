using Spfx.Utilities;
using System;
using System.IO;

namespace Spfx.Io
{
    /// <summary>
    /// Either contains a non-null 'Code' value, or contains a valid 'Data' stream.
    /// </summary>
    public struct StreamOrCode : IDisposable
    {
        private readonly int m_code;
        private Stream m_data;

        public Stream Data
        {
            get
            {
                if (m_data is null)
                    throw new InvalidOperationException("The frame did not contain data");
                return m_data;
            }
        }

        public int? Code
        {
            get
            {
                if (m_data is null)
                    return m_code;
                return null;
            }
        }

        public StreamOrCode(Stream data)
        {
            Guard.ArgumentNotNull(data, nameof(data));
            m_data = data;
            m_code = 0;
        }

        public static StreamOrCode CreateFromRentedArray(byte[] bytes, int count)
        {
            return new StreamOrCode(RentedMemoryStream.CreateFromRentedArray(bytes, count, false));
        }

        public StreamOrCode(int code)
        {
            m_data = null;
            m_code = code;
        }

        public void Dispose()
        {
            m_data?.Dispose();
        }

        internal Stream AcquireData()
        {
            var d = Data;
            m_data = null;
            return d;
        }
    }
}
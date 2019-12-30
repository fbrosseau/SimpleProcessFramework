#define DEBUG_DISPOSE_LEAK
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal sealed class RentedMemoryStream : AbstractSyncStream
    {
        private static readonly byte[] s_fallbackBuffer = Array.Empty<byte>();
        private byte[] m_buffer = s_fallbackBuffer;
        private int m_count;
        private int m_position;
        private bool m_disposed;

#if DEBUG_DISPOSE_LEAK
        private StackTrace m_allocationStack = new StackTrace();
#endif

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override long Length => m_count;

        public override bool CanWrite { get; }
        public override long Position
        {
            get => m_position;
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public int Capacity { get; private set; }

        public RentedMemoryStream()
        {
            CanWrite = true;
        }

        public RentedMemoryStream(int capacity)
        {
            CanWrite = true;
            EnsureCapacity(capacity);
        }

        private RentedMemoryStream(byte[] array, int count, bool readOnly)
        {
            CanWrite = !readOnly;
            m_buffer = array;
            m_count = count;
            Capacity = count;
        }

        public static RentedMemoryStream CreateFromRentedArray(byte[] array, int count, bool readOnly)
        {
            return new RentedMemoryStream(array, count, readOnly);
        }

#if DEBUG
        ~RentedMemoryStream()
        {
            if (AppDomain.CurrentDomain.IsFinalizingForUnload() || Environment.HasShutdownStarted)
                return;

#if DEBUG_DISPOSE_LEAK
            Debug.Fail("Stream was not disposed: " + m_allocationStack);
#else
            Debug.Fail("Stream was not disposed");
#endif
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                var buf = Interlocked.Exchange(ref m_buffer, s_fallbackBuffer);
                if (buf.Length > 0)
                    ArrayPool<byte>.Shared.Return(buf);

                m_disposed = true;
            }

            base.Dispose(disposing);
        }

        public void EnsureCapacity(int capacity)
        {
            EnsureNotReadOnly();

            if (capacity <= Capacity)
                return;

            var newBuffer = ArrayPool<byte>.Shared.Rent(capacity);
            capacity = newBuffer.Length;

            var oldbuffer = m_buffer;
            if (oldbuffer.Length > 0)
            {
                Array.Copy(oldbuffer, 0, newBuffer, 0, m_count);
                ArrayPool<byte>.Shared.Return(oldbuffer);
            }

            m_buffer = newBuffer;

            // a lot less bug prone to keep the behavior of always having brand new bytes in the entire array
            new Span<byte>(newBuffer, m_count, capacity - m_count).Fill(0);

            Capacity = capacity;
        }

        public Span<byte> AsSpan()
        {
            EnsureNotReadOnly();
            return new Span<byte>(m_buffer, 0, m_count);
        }

        public ReadOnlySpan<byte> AsReadOnlySpan()
        {
            EnsureNotDisposed();
            return new ReadOnlySpan<byte>(m_buffer, 0, m_count);
        }

        public byte[] ToArray()
        {
            return AsReadOnlySpan().ToArray();
        }

        public override void Flush()
        {
            EnsureNotReadOnly();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var int32Ofs = checked((int)offset);

            void SetFinalPosition(int val)
            {
                if (val < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset));

                m_position = val;
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    SetFinalPosition(int32Ofs);
                    break;
                case SeekOrigin.Current:
                    SetFinalPosition(checked(m_position + int32Ofs));
                    break;
                case SeekOrigin.End:
                    SetFinalPosition(m_count + int32Ofs);
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            EnsureNotReadOnly();

            int int32Value = checked((int)value);

            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            EnsureCapacity(int32Value);
            m_count = int32Value;
        }

        public override int ReadByte()
        {
            EnsureNotDisposed();

            if (m_position >= m_count)
                return -1;

            return m_buffer[m_position++];
        }

        public override int Read(Span<byte> buffer)
        {
            EnsureNotDisposed();
            var available = Math.Min(buffer.Length, m_count - m_position);
            new Span<byte>(m_buffer, m_position, available).CopyTo(buffer);
            m_position += available;
            return available;
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ct)
        {
            await destination.WriteAsync(m_buffer, m_position, m_count, ct).ConfigureAwait(false);
            m_position += m_count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureNotReadOnly();

            if (buffer.Length == 0)
                return;

            EnsureCapacity(m_position + buffer.Length);
            var dest = new Span<byte>(m_buffer, m_position, buffer.Length);
            buffer.CopyTo(dest);
            m_position += buffer.Length;
            if (m_position > m_count)
                m_count = m_position;
        }

        public override void WriteByte(byte value)
        {
            EnsureNotReadOnly();

            EnsureCapacity(m_position + 1);
            m_buffer[m_position++] = value;
            if (m_position > m_count)
                m_count = m_position;
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            destination.Write(m_buffer, m_position, m_count);
            m_position += m_count;
        }

        private void EnsureNotReadOnly()
        {
            EnsureNotDisposed();
            if (!CanWrite)
                throw new InvalidOperationException("This stream is read-only");
        }

        private void EnsureNotDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(RentedMemoryStream));
        }
    }
}
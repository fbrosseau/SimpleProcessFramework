using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal static class StreamExtensions
    {
        public static async ValueTask<Stream> ReadLengthPrefixedBlockAsync(this Stream stream, int maximumSize = int.MaxValue, CancellationToken ct = default)
        {
            var buf = ArrayPool<byte>.Shared.Rent(2048);
            bool freeBuffer = true;
            try
            {
                await ReadAllBytesAsync(stream, new ArraySegment<byte>(buf, 0, 4), ct).ConfigureAwait(false);

                int size = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(buf, 0, 4));
                if (size > maximumSize || size < 0)
                    throw new SerializationException("Received a message larger than the maximum allowed size");

                if (size == 0)
                    return Stream.Null;

                if (buf.Length < size)
                {
                    ArrayPool<byte>.Shared.Return(buf);
                    buf = ArrayPool<byte>.Shared.Rent(size);
                }

                await ReadAllBytesAsync(stream, new ArraySegment<byte>(buf, 0, size), ct).ConfigureAwait(false);
                var outputStream = RentedMemoryStream.CreateFromRentedArray(buf, size, false);
                freeBuffer = false;
                return outputStream;
            }
            finally
            {
                if (freeBuffer)
                    ArrayPool<byte>.Shared.Return(buf);
            }
        }

        public static void ReadAllBytes(this Stream stream, ArraySegment<byte> buf)
        {
            int count = buf.Count;
            int ofs = 0;

            while (count > 0)
            {
                int read = stream.Read(buf.Array, buf.Offset + ofs, count);
                if (read < 1)
                    throw new EndOfStreamException();

                ofs += read;
                count -= read;
            }
        }

        public static async ValueTask ReadAllBytesAsync(this Stream stream, ArraySegment<byte> buf, CancellationToken ct = default)
        {
            int count = buf.Count;
            int ofs = 0;

            while (count > 0)
            {
                int read = await stream.ReadAsync(buf.Array, buf.Offset + ofs, count, ct).ConfigureAwait(false);
                if (read < 1)
                    throw new EndOfStreamException();

                ofs += read;
                count -= read;
            }
        }

        internal static byte[] CopyToByteArray(Stream s)
        {
            if (s is MemoryStream ms)
                return ms.ToArray();
            if (s is RentedMemoryStream rms)
                return rms.ToArray();

            var buf = new byte[s.Length];
            ReadAllBytes(s, new ArraySegment<byte>(buf));
            return buf;
        }
    }
}
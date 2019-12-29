using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal static class StreamExtensions
    {
        public static async ValueTask<Stream> ReadLengthPrefixedBlock(this Stream stream, int maximumSize = int.MaxValue, CancellationToken ct = default)
        {
            var buf = new byte[4];
            await ReadAllBytesAsync(stream, new ArraySegment<byte>(buf), ct);

            int size = BinaryPrimitives.ReadInt32LittleEndian(buf);
            if (size > maximumSize || size < 0)
                throw new SerializationException("Received a message larger than the maximum allowed size");

            if (size == 0)
                return Stream.Null;

            Array.Resize(ref buf, size);

            await ReadAllBytesAsync(stream, new ArraySegment<byte>(buf), ct);
            return new MemoryStream(buf);
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
    }
}
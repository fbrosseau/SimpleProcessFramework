using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal static class StreamExtensions
    {
        public static async ValueTask<Stream> ReadLengthPrefixedBlock(this Stream stream, int maximumSize = int.MaxValue)
        {
            var buf = new byte[4];
            await ReadBytesAsync(stream, new ArraySegment<byte>(buf));

            int size = BitConverter.ToInt32(buf, 0);
            if (size > maximumSize || size < 0)
                throw new SerializationException("Received a message larger than the maximum allowed size");

            if (size == 0)
                return Stream.Null;

            Array.Resize(ref buf, size);

            await ReadBytesAsync(stream, new ArraySegment<byte>(buf));
            return new MemoryStream(buf);
        }

        public static void ReadBytes(this Stream stream, ArraySegment<byte> buf)
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

        public static async ValueTask ReadBytesAsync(this Stream stream, ArraySegment<byte> buf)
        {
            int count = buf.Count;
            int ofs = 0;

            while (count > 0)
            {
                int read = await stream.ReadAsync(buf.Array, buf.Offset + ofs, count);
                if (read < 1)
                    throw new EndOfStreamException();

                ofs += read;
                count -= read;
            }
        }
    }
}

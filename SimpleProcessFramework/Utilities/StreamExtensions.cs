using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Utilities
{
    internal static class StreamExtensions
    {
        public static async ValueTask<Stream> ReadLengthPrefixedBlock(this Stream stream)
        {
            var buf = new byte[4];
            await ReadBytes(stream, new ArraySegment<byte>(buf));

            Array.Resize(ref buf, BitConverter.ToInt32(buf, 0));

            await ReadBytes(stream, new ArraySegment<byte>(buf));
            return new MemoryStream(buf);
        }

        public static async ValueTask ReadBytes(this Stream stream, ArraySegment<byte> buf)
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

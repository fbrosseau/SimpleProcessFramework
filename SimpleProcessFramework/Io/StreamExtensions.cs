using Spfx.Utilities;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal static class StreamExtensions
    {
        public static async ValueTask<int> ReadLittleEndian32BitInt(this Stream stream, CancellationToken ct = default)
        {
            var buf = new byte[4];
            await stream.ReadAllBytesAsync(new ArraySegment<byte>(buf), ct);
            return BinaryPrimitives.ReadInt32LittleEndian(buf);
        }

        /// <summary>
        /// Expects either a little-endian 32-bit size followed by that amount of bytes, or a zero-or-negative little-endian 32-bit code.
        /// </summary>
        public static async ValueTask<StreamOrCode> ReadCodeOrLengthPrefixedBlockAsync(this Stream stream, int maximumSize = int.MaxValue, byte[] sizeBuffer = null, CancellationToken ct = default)
        {
            bool mustReturnBuffer = false;

            byte[] buf = sizeBuffer;

            if (!(buf?.Length >= 4))
            {
                buf = ArrayPool<byte>.Shared.Rent(4);
                mustReturnBuffer = true;
            }

            try
            {
                bool success = await stream.ReadAllBytesAsync(new ArraySegment<byte>(buf, 0, 4), ct, allowEof: true).ConfigureAwait(false);
                if (!success)
                    return StreamOrCode.CreateEof();

                int size = BinaryPrimitives.ReadInt32LittleEndian(buf);
                if (size > maximumSize)
                    throw new SerializationException("Received a message larger than the maximum allowed size");
                if (size <= 0)
                    return new StreamOrCode(size);

                if (buf.Length < size || !mustReturnBuffer)
                {
                    if (mustReturnBuffer)
                        ArrayPool<byte>.Shared.Return(buf);
                    buf = ArrayPool<byte>.Shared.Rent(size);
                    mustReturnBuffer = true;
                }

                await stream.ReadAllBytesAsync(new ArraySegment<byte>(buf, 0, size), ct).ConfigureAwait(false);
                var outputStream = RentedMemoryStream.CreateFromRentedArray(buf, size, false);
                mustReturnBuffer = false;
                return new StreamOrCode(outputStream);
            }
            finally
            {
                if (mustReturnBuffer)
                    ArrayPool<byte>.Shared.Return(buf);
            }
        }

        public static async ValueTask<Stream> ReadLengthPrefixedBlockAsync(this Stream stream, int maximumSize = int.MaxValue, byte[] sizeBuffer = null, CancellationToken ct = default)
        {
            var res = await stream.ReadCodeOrLengthPrefixedBlockAsync(maximumSize, sizeBuffer, ct);
            if (res.Code == 0)
                return Stream.Null;
            if (res.Code != null)
                BadCodeAssert.ThrowInvalidOperation("Expected to receive data");
            return res.Data;
        }

        public static async ValueTask<bool> ReadAllBytesAsync(this Stream stream, ArraySegment<byte> buf, CancellationToken ct = default, bool allowEof = false)
        {
            int count = buf.Count;
            int ofs = 0;

            while (count > 0)
            {
#if NETCOREAPP || NETSTANDARD2_1_PLUS
                int read = await stream.ReadAsync(new Memory<byte>(buf.Array, buf.Offset + ofs, count), ct).ConfigureAwait(false);
#else
                int read = await stream.ReadAsync(buf.Array, buf.Offset + ofs, count, ct).ConfigureAwait(false);
#endif
                if (read < 1)
                {
                    if (read == 0 && allowEof)
                        return false;

                    throw new EndOfStreamException();
                }

                ofs += read;
                count -= read;
            }

            return true;
        }

        /// <summary>
        /// Expects either a little-endian 32-bit size followed by that amount of bytes, or a zero-or-negative little-endian 32-bit code.
        /// </summary>
        public static StreamOrCode ReadCodeOrLengthPrefixedBlock(this Stream stream, int maximumSize = int.MaxValue, byte[] sizeBuffer = null)
        {
            bool mustReturnBuffer = false;

            byte[] buf = sizeBuffer;

            if (!(buf?.Length >= 4))
            {
                buf = ArrayPool<byte>.Shared.Rent(4);
                mustReturnBuffer = true;
            }

            try
            {
                stream.ReadAllBytes(new ArraySegment<byte>(buf, 0, 4));

                int size = BinaryPrimitives.ReadInt32LittleEndian(buf);
                if (size > maximumSize)
                    throw new SerializationException("Received a message larger than the maximum allowed size");
                if (size <= 0)
                    return new StreamOrCode(size);

                if (buf.Length < size || !mustReturnBuffer)
                {
                    if (mustReturnBuffer)
                        ArrayPool<byte>.Shared.Return(buf);
                    buf = ArrayPool<byte>.Shared.Rent(size);
                    mustReturnBuffer = true;
                }

                stream.ReadAllBytes(new ArraySegment<byte>(buf, 0, size));
                var outputStream = RentedMemoryStream.CreateFromRentedArray(buf, size, false);
                mustReturnBuffer = false;
                return new StreamOrCode(outputStream);
            }
            finally
            {
                if (mustReturnBuffer)
                    ArrayPool<byte>.Shared.Return(buf);
            }
        }

        public static Stream ReadLengthPrefixedBlock(this Stream stream, int maximumSize = int.MaxValue, byte[] sizeBuffer = null)
        {
            var res = stream.ReadCodeOrLengthPrefixedBlock(maximumSize, sizeBuffer);
            if (res.Code == 0)
                return Stream.Null;
            if (res.Code != null)
                BadCodeAssert.ThrowInvalidOperation("Expected to receive data");
            return res.Data;
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

        internal static byte[] CopyToByteArray(Stream s)
        {
            if (s is MemoryStream ms)
                return ms.ToArray();
            if (s is RentedMemoryStream rms)
                return rms.ToArray();

            var buf = new byte[s.Length];
            s.ReadAllBytes(new ArraySegment<byte>(buf));
            return buf;
        }
    }
}
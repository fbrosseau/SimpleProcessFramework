using Spfx.Utilities;
using System;
using System.Buffers;
using System.IO;

namespace Spfx.Serialization
{
    internal sealed class RentedStreamSerializerBinaryReader : SerializerBinaryReader
    {
        private readonly RentedMemoryStream m_stream;

        public RentedStreamSerializerBinaryReader(RentedMemoryStream input)
            : base(input)
        {
            m_stream = input;
        }

        public sealed override int Read(Span<byte> bytes)
        {
            return m_stream.Read(bytes);
        }

        protected sealed override unsafe T ReadBlittable<T>()
        {
            T val = default;
            byte* bytes = (byte*)&val;
            ReadAll(new Span<byte>(bytes, sizeof(T)));
            return val;
        }

        public sealed override byte ReadByte() => m_stream.ReadByteOrThrow();
        public sealed override short ReadInt16() => ReadBlittable<short>();
        public sealed override ushort ReadUInt16() => ReadBlittable<ushort>();
        public sealed override uint ReadUInt32() => ReadBlittable<uint>();
        public sealed override int ReadInt32() => ReadBlittable<int>();
        public sealed override unsafe long ReadInt64() => ReadBlittable<long>();
        public sealed override unsafe ulong ReadUInt64() => ReadBlittable<ulong>();
        public sealed override unsafe Guid ReadGuid() => ReadBlittable<Guid>();
    }

    internal class SerializerBinaryReader : BinaryReader
    {
        protected SerializerBinaryReader(Stream input)
            : base(input)
        {
        }

        public uint ReadEncodedUInt32()
        {
            const byte highBit = 0x80;
            const byte dataBits = 0x7F;

            uint val = 0;
            int shift = 0;
            byte b;
            do
            {
                b = ReadByte();
                val |= (uint)((b & dataBits) << shift);
                shift += 7;
            } while ((b & highBit) != 0);

            return val;
        }

        internal static SerializerBinaryReader Create(Stream s)
        {
            if (s is RentedMemoryStream rms)
                return new RentedStreamSerializerBinaryReader(rms);
            return new SerializerBinaryReader(s);
        }

        public int ReadEncodedInt32()
        {
            return Zag(ReadEncodedUInt32());
        }

        internal static int Zag(uint v)
        {
            return unchecked((int)((v >> 1) ^ (-(v & 1))));
        }

#if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
        public override int Read(Span<byte> buffer)
#else
        public virtual int Read(Span<byte> buffer)
#endif
        {
#if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
            return BaseStream.Read(buffer);
#else
            var buf = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var count = Read(buf, 0, buffer.Length);
                if (count > 0)
                    new Span<byte>(buf, 0, count).CopyTo(buffer);
                return count;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
#endif
        }

        protected virtual unsafe T ReadBlittable<T>()
            where T : unmanaged
        {
            T val = default;
            byte* bytes = (byte*)&val;
            Read(new Span<byte>(bytes, sizeof(T)));
            return val;
        }

        public virtual void ReadAll(Span<byte> bytes)
        {
            var remainingSpan = bytes;
            while (remainingSpan.Length > 0)
            {
                int len = Read(bytes);
                if (len == 0)
                    throw new EndOfStreamException();

                remainingSpan = remainingSpan.Slice(len);
            }
        }

        public void ReadAll(byte[] bytes, int offset, int count)
        {
            while (count > 0)
            {
                int len = Read(bytes, offset, count);
                if (len == 0)
                    throw new EndOfStreamException();

                offset += len;
                count -= len;
            }
        }

        public override short ReadInt16() => ReadBlittable<short>();
        public override ushort ReadUInt16() => ReadBlittable<ushort>();
        public override uint ReadUInt32() => ReadBlittable<uint>();
        public override int ReadInt32() => ReadBlittable<int>();
        public override unsafe long ReadInt64() => ReadBlittable<long>();
        public override unsafe ulong ReadUInt64() => ReadBlittable<ulong>();
        public virtual unsafe Guid ReadGuid() => ReadBlittable<Guid>();
    }
}
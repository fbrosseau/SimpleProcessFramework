using Spfx.Utilities;
using System;
using System.Buffers;
using System.IO;

namespace Spfx.Serialization
{
    internal sealed class RentedStreamSerializerBinaryWriter : SerializerBinaryWriter
    {
        private readonly RentedMemoryStream m_rentedStream;

        public RentedStreamSerializerBinaryWriter(RentedMemoryStream output)
            : base(output)
        {
            m_rentedStream = output;
        }

        public sealed override void Write(ReadOnlySpan<byte> bytes)
        {
            m_rentedStream.Write(bytes);
        }

        protected sealed override unsafe void WriteBlittable<T>(T val)
        {
            byte* bytes = (byte*)&val;
            Write(new Span<byte>(bytes, sizeof(T)));
        }

        public sealed override void Write(short val) => WriteBlittable(val);
        public sealed override void Write(ushort val) => WriteBlittable(val);
        public sealed override void Write(int val) => WriteBlittable(val);
        public sealed override void Write(uint val) => WriteBlittable(val);
        public sealed override void Write(long val) => WriteBlittable(val);
        public sealed override void Write(ulong val) => WriteBlittable(val);
        public sealed override void Write(Guid val) => WriteBlittable(val);
    }

    internal class SerializerBinaryWriter : BinaryWriter
    {
        protected SerializerBinaryWriter(Stream output)
            : base(output)
        {
        }

        public void WriteEncodedUInt32(uint v)
        {
            const byte highBit = 0x80;

            while (v >= highBit)
            {
                Write((byte)(v | highBit));
                v >>= 7;
            }

            Write((byte)v);
        }

        public void WriteEncodedInt32(int v)
        {
            WriteEncodedUInt32(Zig(v));
        }

        internal static uint Zig(int n)
        {
            return unchecked((uint)((n << 1) ^ (n >> 31)));
        }

        public override void Write(short val) => WriteBlittable(val);
        public override void Write(ushort val) => WriteBlittable(val);
        public override void Write(int val) => WriteBlittable(val);
        public override void Write(uint val) => WriteBlittable(val);
        public override void Write(long val) => WriteBlittable(val);
        public override void Write(ulong val) => WriteBlittable(val);
        public virtual void Write(Guid val) => WriteBlittable(val);

        protected virtual unsafe void WriteBlittable<T>(T val)
            where T : unmanaged
        {
            byte* bytes = (byte*)&val;
            Write(new Span<byte>(bytes, sizeof(T)));
        }

#if NETSTANDARD2_1_PLUS || NETCOREAPP
        public override void Write(ReadOnlySpan<byte> bytes)
#else
        public virtual void Write(ReadOnlySpan<byte> bytes)
#endif
        {
#if NETSTANDARD2_1_PLUS || NETCOREAPP
            OutStream.Write(bytes);
#else
            if (bytes.Length == 0)
                return;

            var buf = ArrayPool<byte>.Shared.Rent(bytes.Length);
            try
            {
                bytes.CopyTo(buf);
                Write(buf);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
#endif
        }

        internal static SerializerBinaryWriter Create(Stream ms)
        {
            if (ms is RentedMemoryStream rms)
                return new RentedStreamSerializerBinaryWriter(rms);
            return new SerializerBinaryWriter(ms);
        }
    }
}
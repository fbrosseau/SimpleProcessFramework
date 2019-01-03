using System.IO;

namespace SimpleProcessFramework.Serialization
{
    internal class SerializerBinaryWriter : BinaryWriter
    {
        public SerializerBinaryWriter(Stream output)
            : base(output)
        {
        }

        public void WriteEncodedUInt32(uint v)
        {
            const byte highBit = 0x80;

            while(v >= highBit)
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
    }
}
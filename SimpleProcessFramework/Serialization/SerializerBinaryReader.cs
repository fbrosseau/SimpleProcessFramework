using System.IO;

namespace SimpleProcessFramework.Serialization
{
    internal class SerializerBinaryReader : BinaryReader
    {
        public SerializerBinaryReader(Stream input) 
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

        public int ReadEncodedInt32()
        {
            return Zag(ReadEncodedUInt32());
        }

        internal static int Zag(uint v)
        {
            return unchecked((int)((v >> 1) ^ (-(v & 1))));
        }
    }
}
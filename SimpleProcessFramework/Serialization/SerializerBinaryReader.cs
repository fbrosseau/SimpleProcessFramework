using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Spfx.Serialization
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

        #if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
        public void ReadAll(Span<byte> bytes)
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
        #endif

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

        public unsafe Guid ReadGuid()
        {
            Guid g = default;
            long* int64 = (long*)&g;
            int64[0] = ReadInt64();
            int64[1] = ReadInt64();
            return g;
        }
    }
}
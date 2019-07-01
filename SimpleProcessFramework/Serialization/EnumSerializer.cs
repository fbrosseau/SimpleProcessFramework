using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    internal class EnumSerializer : ITypeSerializer
    {
        private readonly int m_underlyingTypeBytes;
        private readonly Type m_enumType;

        private EnumSerializer(Type enumType)
        {
            var underlying = Enum.GetUnderlyingType(enumType);
            m_underlyingTypeBytes = Marshal.SizeOf(underlying);
            m_enumType = enumType;
        }

        public static ITypeSerializer Create(Type enumType)
        {
            return new EnumSerializer(enumType);
        }

        public object ReadObject(DeserializerSession reader)
        {
            switch(m_underlyingTypeBytes)
            {
                case 1:
                    return Enum.ToObject(m_enumType, reader.Reader.ReadByte());
                case 2:
                    return Enum.ToObject(m_enumType, reader.Reader.ReadUInt16());
                case 4:
                    return Enum.ToObject(m_enumType, reader.Reader.ReadUInt32());
                case 8:
                    return Enum.ToObject(m_enumType, reader.Reader.ReadUInt64());
                default:
                    throw new SerializationException("Invalid enum size");
            }
        }

        public void WriteObject(SerializerSession bw, object graph)
        {
            switch (m_underlyingTypeBytes)
            {
                case 1:
                    bw.Writer.Write(Convert.ToByte(graph));
                    break;
                case 2:
                    bw.Writer.Write(Convert.ToUInt16(graph));
                    break;
                case 4:
                    bw.Writer.Write(Convert.ToUInt32(graph));
                    break;
                case 8:
                    bw.Writer.Write(Convert.ToUInt64(graph));
                    break;
                default:
                    throw new SerializationException("Invalid enum size");
            }
        }
    }
}
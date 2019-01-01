using System;
using System.IO;

namespace SimpleProcessFramework.Serialization
{
    internal class SerializerSession
    {
        public Stream Stream { get; }
        public BinaryWriter Writer { get; }

        public SerializerSession(Stream ms)
        {
            Stream = ms;
            Writer = new BinaryWriter(ms);
        }

        internal void WriteType(Type actualType)
        {
            Stream.WriteByte((byte)DataKind.Type);
            Writer.Write(actualType.AssemblyQualifiedName);
        }
    }
}
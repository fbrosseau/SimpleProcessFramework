using Spfx.Reflection;
using System;
using System.IO;

namespace Spfx.Serialization
{
    internal class SerializerSession
    {
        public Stream Stream { get; }
        public SerializerBinaryWriter Writer { get; }

        private readonly SerializerReferencesCache m_localReferences = new SerializerReferencesCache(SerializerReferencesCache.HardcodedReferences);
        private long m_originalBaseLocation;

        public SerializerSession(Stream ms)
        {
            Stream = ms;
            Writer = new SerializerBinaryWriter(ms);
        }

        internal void WriteType(Type actualType)
        {
            WriteMetadata(DataKind.Type);
            WriteReference((ReflectedTypeInfo)actualType);
        }

        internal void BeginSerialization()
        {
            Writer.Write(DefaultBinarySerializer.MagicHeader);

            m_originalBaseLocation = Stream.Position;
            Stream.Position += 4;
        }

        internal void FinishSerialization()
        {
            WritePositionDelta(m_originalBaseLocation);

            var newPosition = Stream.Position;
            Stream.Position += 4;

            m_localReferences.WriteAllReferences(this);
            WritePositionDelta(newPosition);
        }

        internal void WriteReference(object obj)
        {
            if (obj is null)
            {
                WriteMetadata(DataKind.Null);
                return;
            }

            int index = m_localReferences.GetOrCreateCacheIndex(obj);

            WriteMetadata(DataKind.Ref);
            Writer.WriteEncodedInt32(index);
        }

        internal void WriteMetadata(DataKind marker)
        {
            Stream.WriteByte((byte)marker);
        }

        internal void WritePositionDelta(long originalPosition)
        {
            var newLocation = Stream.Position;
            Stream.Position = originalPosition;
            Writer.Write(checked((int)(newLocation - originalPosition - 4)));
            Writer.Flush();
            Stream.Position = newLocation;
        }
    }
}
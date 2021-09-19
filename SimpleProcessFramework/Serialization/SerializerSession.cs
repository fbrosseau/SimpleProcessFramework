using Spfx.Reflection;
using System;
using System.IO;

namespace Spfx.Serialization
{
    public class SerializerSession
    {
        public Stream Stream { get; }
        public SerializerBinaryWriter Writer { get; }
        public bool CanAddReferences => !m_localReferences.IsFrozen;

        private readonly SerializerReferencesCache m_localReferences = new SerializerReferencesCache(SerializerReferencesCache.HardcodedReferences);
        private PositionDeltaScope m_sizeScope;

        public SerializerSession(Stream ms)
        {
            Stream = ms;
            Writer = SerializerBinaryWriter.Create(ms);
        }

        internal void WriteType(Type actualType)
        {
            WriteMetadata(DataKind.Type);
            WriteReference((ReflectedTypeInfo)actualType);
        }

        internal void BeginSerialization()
        {
            Writer.Write(DefaultBinarySerializer.MagicHeader);
            m_sizeScope = CreatePositionDeltaScope();
        }

        internal void FinishSerialization()
        {
            m_sizeScope.Dispose();

            using var refsDelta = CreatePositionDeltaScope();
            m_localReferences.WriteAllReferences(this);
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

        public struct PositionDeltaScope : IDisposable
        {
            private readonly SerializerSession m_serializerSession;
            private readonly long m_locationBeforeMember;

            public PositionDeltaScope(SerializerSession serializerSession)
            {
                m_serializerSession = serializerSession;

                var stream = m_serializerSession.Stream;
                m_locationBeforeMember = stream.Position;
                stream.Position += 4;
            }

            public void Dispose()
            {
                m_serializerSession.WritePositionDelta(m_locationBeforeMember);
            }
        }

        internal PositionDeltaScope CreatePositionDeltaScope()
        {
            return new PositionDeltaScope(this);
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
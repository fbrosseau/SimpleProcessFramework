using System;
using System.IO;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    internal class DeserializerSession
    {
        public Stream Stream { get; }
        public SerializerBinaryReader Reader { get; }

        private DeserializerReferencesCache m_localReferences;
        private long m_finalPosition;

        public DeserializerSession(Stream s)
        {
            Stream = s;
            Reader = new SerializerBinaryReader(s);
        }
        
        internal object ReadReference(bool readHeader)
        {
            if (readHeader)
            {
                var token = ReadMetadata();
                if (token == DataKind.Null)
                    return null;
                if (token != DataKind.Ref)
                    throw new SerializationException("Data is invalid");
            }

            int referenceId = Reader.ReadEncodedInt32();
            return m_localReferences.GetObject(referenceId, mustExist: true);
        }

        private DataKind ReadMetadata()
        {
            return (DataKind)Stream.ReadByte();
        }

        internal void PrepareRead()
        {
            if (Reader.ReadInt32() != DefaultBinarySerializer.MagicHeader)
                throw new SerializationException("Data is invalid");

            var totalGraphSize = Reader.ReadInt32();
            var graphPosition = Stream.Position;
            Stream.Position = graphPosition + totalGraphSize;

            var referencesBlockSize = Reader.ReadInt32();
            m_finalPosition = Stream.Position + referencesBlockSize;

            if (referencesBlockSize == 0)
            {
                m_localReferences = DeserializerReferencesCache.HardcodedReferences;
            }
            else
            {
                m_localReferences = new DeserializerReferencesCache(DeserializerReferencesCache.HardcodedReferences);
                var totalReferences = Reader.ReadEncodedUInt32();

                for (uint i = 0; i < totalReferences; ++i)
                {
                    var idx = Reader.ReadEncodedInt32();
                    var obj = DefaultBinarySerializer.Deserialize(this, typeof(object));
                    m_localReferences.SetReferenceKey(obj, idx);
                }
            }

            Stream.Position = graphPosition;
        }

        internal void EndRead()
        {
            Stream.Position = m_finalPosition;
        }
    }
}
using Spfx.Utilities;
using System;

namespace Spfx.Serialization.Serializers
{
    internal sealed class SimpleTypeSerializer<T> : TypedSerializer<T>
    {
        private readonly Action<SerializerSession, T> m_write;
        private readonly Func<DeserializerSession, T> m_read;

        public SimpleTypeSerializer(Action<SerializerSession, T> write, Func<DeserializerSession, T> read)
        {
            m_write = write;
            m_read = read;
        }

        public sealed override object ReadObject(DeserializerSession reader)
        {
            return BoxHelper.Box(m_read(reader));
        }

        public sealed override void WriteObject(SerializerSession bw, object graph)
        {
            WriteObject(bw, (T)graph);
        }

        public sealed override T ReadTypedObject(DeserializerSession session)
        {
            return m_read(session);
        }

        public sealed override void WriteObject(SerializerSession session, T graph)
        {
            m_write(session, graph);
        }
    }
}
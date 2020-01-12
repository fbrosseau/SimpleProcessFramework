using Spfx.Utilities;
using System;

namespace Spfx.Serialization.Serializers
{
    internal static class NullableSerializer
    {
        internal const byte NullNullable = (byte)DataKind.Null;
        internal const byte ValidNullable = 0xBB;

        public static ITypeSerializer Create(Type elementType)
        {
            return (ITypeSerializer)Activator.CreateInstance(typeof(NullableSerializer<>).MakeGenericType(elementType));
        }
    }

    internal sealed class NullableSerializer<T> : TypedSerializer<T?>
        where T : struct
    {
        private readonly ITypeSerializer<T> m_baseSerializer;

        public NullableSerializer()
        {
            m_baseSerializer = DefaultBinarySerializer.GetSerializer<T>();
        }

        public sealed override T? ReadTypedObject(DeserializerSession session)
        {
            var b = session.Reader.ReadByte();
            if (b == NullableSerializer.NullNullable)
                return null;
            if (b == NullableSerializer.ValidNullable)
                return m_baseSerializer.ReadTypedObject(session);
            throw DefaultBinarySerializer.ThrowBadSerializationException();
        }

        public sealed override object ReadObject(DeserializerSession session)
        {
            return BoxHelper.Box(ReadTypedObject(session));
        }

        public sealed override void WriteObject(SerializerSession session, object graph) => WriteObject(session, graph as T?);

        public sealed override void WriteObject(SerializerSession session, T? graph)
        {
            if (graph is null)
            {
                session.Writer.Write(NullableSerializer.NullNullable);
            }
            else
            {
                session.Writer.Write(NullableSerializer.ValidNullable);
                m_baseSerializer.WriteObject(session, graph.Value);
            }
        }
    }
}
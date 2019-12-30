using Spfx.Utilities;

namespace Spfx.Serialization.Serializers
{
    internal sealed class BoolSerializer : TypedSerializer<bool>
    {
        public const byte TrueByte = 0x11;
        public const byte FalseByte = 0x22;
        public const byte NullByte = Serializers.NullableSerializer.NullNullable;

        public static BoolSerializer Serializer { get; } = new BoolSerializer();
        public static NullableBoolSerializer NullableSerializer { get; } = new NullableBoolSerializer();

        public sealed override object ReadObject(DeserializerSession session) => ReadBoolObject(session);
        public sealed override bool ReadTypedObject(DeserializerSession session) => ReadBool(session);
        public override void WriteObject(SerializerSession session, object graph) => WriteBool(session, graph);
        public override void WriteObject(SerializerSession session, bool graph) => WriteBool(session, graph);

        internal static object ReadNullableBoolObject(DeserializerSession session) => BoxHelper.Box(ReadNullableBool(session));
        internal static object ReadBoolObject(DeserializerSession session) => BoxHelper.Box(ReadBool(session));
        internal static void WriteBool(SerializerSession session, object graph) => WriteBool(session, (bool)graph);
        internal static void WriteNullableBool(SerializerSession session, object graph) => WriteNullableBool(session, (bool?)graph);
        internal static void WriteNullableBool(SerializerSession session, bool? graph)
        {
            if (graph is null)
                session.Writer.Write(NullByte);
            else
                WriteBool(session, graph.Value);
        }

        internal static void WriteBool(SerializerSession session, bool b)
        {
            session.Writer.Write(b ? TrueByte : FalseByte);
        }

        internal static bool? ReadNullableBool(DeserializerSession session)
        {
            var b = session.Reader.ReadByte();
            if (b == TrueByte)
                return true;
            if (b == FalseByte)
                return false;
            if (b == NullByte)
                return null;
            throw DefaultBinarySerializer.ThrowBadSerializationException();
        }

        internal static bool ReadBool(DeserializerSession session)
        {
            var b = session.Reader.ReadByte();
            if (b == TrueByte)
                return true;
            if (b == FalseByte)
                return false;
            throw DefaultBinarySerializer.ThrowBadSerializationException();
        }

        public class NullableBoolSerializer : TypedSerializer<bool?>
        {
            public override object ReadObject(DeserializerSession session) => ReadNullableBoolObject(session);
            public override bool? ReadTypedObject(DeserializerSession session) => ReadNullableBool(session);
            public override void WriteObject(SerializerSession session, object graph) => WriteNullableBool(session, graph);
            public override void WriteObject(SerializerSession session, bool? graph) => WriteNullableBool(session, graph);
        }
    }
}

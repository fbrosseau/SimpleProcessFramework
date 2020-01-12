namespace Spfx.Serialization.Serializers
{
    internal class TypedSerializerWrapper<T> : BaseTypeSerializer, ITypeSerializer<T>
    {
        private readonly ITypeSerializer m_baseSerializer;

        public TypedSerializerWrapper(ITypeSerializer typedSerializer)
        {
            m_baseSerializer = typedSerializer;
        }

        public override object ReadObject(DeserializerSession reader) => m_baseSerializer.ReadObject(reader);
        public T ReadTypedObject(DeserializerSession session) => (T)ReadObject(session);
        public void WriteObject(SerializerSession session, T graph) => WriteObject(session, (object)graph);
        public override void WriteObject(SerializerSession bw, object graph) => m_baseSerializer.WriteObject(bw, graph);
    }
}
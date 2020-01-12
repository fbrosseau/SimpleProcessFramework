namespace Spfx.Serialization.Serializers
{
    internal interface ITypeSerializer
    {
        void WriteObject(SerializerSession session, object graph);
        void WriteObjectWithHeader(SerializerSession session, object graph);

        object ReadObject(DeserializerSession reader);
    }

    internal interface ITypeSerializer<T> : ITypeSerializer
    {
        void WriteObject(SerializerSession session, T graph);
        T ReadTypedObject(DeserializerSession session);
    }
}
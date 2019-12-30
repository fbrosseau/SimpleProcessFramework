namespace Spfx.Serialization
{
    internal interface ITypeSerializer
    {
        void WriteObject(SerializerSession bw, object graph);
        object ReadObject(DeserializerSession reader);
    }

    internal interface ITypeSerializer<T> : ITypeSerializer
    {
        void WriteObject(SerializerSession session, T graph);
        T ReadTypedObject(DeserializerSession session);
    }
}
namespace Spfx.Serialization
{
    internal interface ITypeSerializer
    {
        void WriteObject(SerializerSession bw, object graph);
        object ReadObject(DeserializerSession reader);
    }
}
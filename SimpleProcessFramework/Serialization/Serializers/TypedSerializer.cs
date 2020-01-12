using Spfx.Reflection;
using Spfx.Utilities;
using System;

namespace Spfx.Serialization.Serializers
{
    internal abstract class BaseTypeSerializer : ITypeSerializer
    {
        public abstract object ReadObject(DeserializerSession reader);
        public abstract void WriteObject(SerializerSession session, object graph);

        public virtual void WriteObjectWithHeader(SerializerSession session, object graph)
        {
            session.WriteMetadata(DataKind.Graph);
            WriteObject(session, graph);
        }
    }

    internal abstract class TypedSerializer<T> : BaseTypeSerializer, ITypeSerializer<T>
    {
        protected readonly Type TypeofT = ReflectionUtilities.GetType<T>();

        public virtual T ReadTypedObject(DeserializerSession session)
        {
            return (T)ReadObject(session);
        }

        public virtual void WriteObject(SerializerSession session, T graph)
        {
            WriteObject(session, BoxHelper.Box(graph));
        }
    }
}
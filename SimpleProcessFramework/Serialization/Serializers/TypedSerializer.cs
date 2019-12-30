using Spfx.Reflection;
using Spfx.Utilities;
using System;

namespace Spfx.Serialization.Serializers
{
    internal abstract class TypedSerializer<T> : ITypeSerializer, ITypeSerializer<T>
    {
        protected readonly Type TypeofT = ReflectionUtilities.GetType<T>();

        public abstract object ReadObject(DeserializerSession session);

        public virtual T ReadTypedObject(DeserializerSession session)
        {
            return (T)ReadObject(session);
        }

        public virtual void WriteObject(SerializerSession session, T graph)
        {
            WriteObject(session, BoxHelper.Box(graph));
        }

        public abstract void WriteObject(SerializerSession bw, object graph);
    }
}
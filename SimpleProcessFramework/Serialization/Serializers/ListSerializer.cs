using System;
using System.Collections.Generic;
using Spfx.Reflection;

namespace Spfx.Serialization.Serializers
{
    internal class ListSerializer
    {
        public static ITypeSerializer Create(Type elementType)
        {
            return (ITypeSerializer)Activator.CreateInstance(typeof(ListSerializer<>).MakeGenericType(elementType));
        }
    }

    internal sealed class ListSerializer<T> : TypedSerializer<List<T>>
    {
        private readonly Type m_typeofElement = ReflectionUtilities.GetType<T>();

        public sealed override object ReadObject(DeserializerSession reader)
        {
            int count = checked((int)reader.Reader.ReadEncodedUInt32());
            var list = new List<T>(count);

            for (int i = 0; i < count; ++i)
            {
                list.Add((T)DefaultBinarySerializer.Deserialize(reader, m_typeofElement));
            }

            return list;
        }

        public sealed override void WriteObject(SerializerSession bw, object graph)
        {
            var arr = (ICollection<T>)graph;
            bw.Writer.WriteEncodedUInt32(checked((uint)arr.Count));

            foreach (var i in arr)
            {
                DefaultBinarySerializer.Serialize(bw, i, m_typeofElement);
            }
        }
    }
}
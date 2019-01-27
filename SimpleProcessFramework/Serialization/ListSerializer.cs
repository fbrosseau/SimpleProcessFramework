using Spfx.Reflection;
using System;
using System.Collections.Generic;

namespace Spfx.Serialization
{
    internal class ListSerializer
    {
        public static ITypeSerializer Create(Type elementType)
        {
            return (ITypeSerializer)Activator.CreateInstance(typeof(ListSerializer<>).MakeGenericType(elementType));
        }
    }

    internal class ListSerializer<T> : ITypeSerializer
    {
        public object ReadObject(DeserializerSession reader)
        {
            int count = reader.Reader.ReadEncodedInt32();
            var list = new List<T>(count);

            for (int i = 0; i < count; ++i)
            {
                list.Add((T)DefaultBinarySerializer.Deserialize(reader, ReflectionUtilities.GetType<T>()));
            }

            return list;
        }

        public void WriteObject(SerializerSession bw, object graph)
        {
            var arr = (ICollection<T>)graph;
            bw.Writer.WriteEncodedInt32(arr.Count);

            foreach (var i in arr)
            {
                DefaultBinarySerializer.Serialize(bw, i, ReflectionUtilities.GetType<T>());
            }
        }
    }
}
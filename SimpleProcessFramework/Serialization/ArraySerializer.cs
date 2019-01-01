﻿using System;

namespace SimpleProcessFramework.Serialization
{
    internal class ArraySerializer
    {
        public static ITypeSerializer Create(Type elementType)
        {
            return (ITypeSerializer)Activator.CreateInstance(typeof(ArraySerializer<>).MakeGenericType(elementType));
        }
    }

    internal class ArraySerializer<T> : ITypeSerializer
    {
        public object ReadObject(DeserializerSession reader)
        {
            int count = reader.Reader.ReadInt32();
            if (count == 0)
                return Array.Empty<T>();

            var arr = new T[count];

            for (int i = 0; i < count; ++i)
            {
                arr[i] = (T)DefaultBinarySerializer.Deserialize(reader, typeof(T));
            }

            return arr;
        }

        public void WriteObject(SerializerSession bw, object graph)
        {
            var arr = (T[])graph;
            bw.Writer.Write(arr.Length);

            foreach (var i in arr)
            {
                DefaultBinarySerializer.Serialize(bw, i, typeof(T));
            }
        }
    }
}
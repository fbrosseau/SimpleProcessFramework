using Spfx.Reflection;
using System;

namespace Spfx.Serialization.Serializers
{
    internal class ArraySerializer
    {
        public static ITypeSerializer Create(Type elementType)
        {
            if (elementType.IsValueType)
            {
                if (ReflectionUtilities.IsBlittable(elementType))
                    return (ITypeSerializer)Activator.CreateInstance(typeof(BlittableArraySerializer<>).MakeGenericType(elementType));
                return (ITypeSerializer)Activator.CreateInstance(typeof(ValueTypeArraySerializer<>).MakeGenericType(elementType));
            }
            return (ITypeSerializer)Activator.CreateInstance(typeof(ArraySerializer<>).MakeGenericType(elementType));
        }
    }

    internal class BlittableArraySerializer<T> : ValueTypeArraySerializer<T>
        where T : unmanaged
    {
        protected override unsafe void DeserializeAll(DeserializerSession reader, T[] arr)
        {
            fixed (T* ptr = &arr[0])
            {
                reader.Reader.ReadAll(new Span<byte>(ptr, arr.Length * sizeof(T)));
            }
        }

        protected override unsafe void SerializeAll(SerializerSession bw, T[] arr)
        {
            fixed (T* ptr = &arr[0])
            {
                bw.Writer.Write(new Span<byte>(ptr, arr.Length * sizeof(T)));
            }
        }
    }

    internal class ValueTypeArraySerializer<T> : ArraySerializer<T>
    {
        protected override void DeserializeAll(DeserializerSession reader, T[] arr)
        {
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = (T)DefaultBinarySerializer.DeserializeExactType(reader, TypeofElement);
            }
        }

        protected override void SerializeAll(SerializerSession bw, T[] arr)
        {
            for (int i = 0; i < arr.Length; ++i)
            {
                DefaultBinarySerializer.SerializeExactType(bw, arr[i], TypeofElement);
            }
        }
    }

    internal class ArraySerializer<T> : TypedSerializer<T[]>
    {
        protected readonly T[] m_empty = Array.Empty<T>();
        protected readonly Type TypeofElement = ReflectionUtilities.GetType<T>();

        public override object ReadObject(DeserializerSession reader)
        {
            int count = checked((int)reader.Reader.ReadEncodedUInt32());
            if (count == 0)
                return m_empty;

            var arr = new T[count];
            DeserializeAll(reader, arr);
            return arr;
        }

        protected virtual void DeserializeAll(DeserializerSession reader, T[] arr)
        {
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = (T)DefaultBinarySerializer.Deserialize(reader, TypeofElement);
            }
        }

        public override void WriteObject(SerializerSession bw, object graph)
        {
            var arr = (T[])graph;
            bw.Writer.WriteEncodedUInt32(checked((uint)arr.Length));

            SerializeAll(bw, arr);
        }

        protected virtual void SerializeAll(SerializerSession bw, T[] arr)
        {
            foreach (var i in arr)
            {
                DefaultBinarySerializer.Serialize(bw, i, TypeofElement);
            }
        }
    }
}
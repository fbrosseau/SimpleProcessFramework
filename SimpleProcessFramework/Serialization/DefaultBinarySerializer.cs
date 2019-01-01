using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

namespace SimpleProcessFramework.Serialization
{
    internal class DefaultBinarySerializer : IBinarySerializer
    {
        private const int MagicHeader = unchecked((int)0xBEEF1234);

        public T Deserialize<T>(Stream s)
        {
            var reader = new DeserializerSession(s);
            if (reader.Reader.ReadInt32() != MagicHeader)
                throw new SerializationException("Data is invalid");

            return (T)Deserialize(reader, typeof(T));
        }

        internal static object Deserialize(DeserializerSession reader, Type expectedType)
        {
            while (true)
            {
                switch ((DataKind)reader.Stream.ReadByte())
                {
                    case DataKind.Null:
                        return null;
                    case DataKind.Type:
                        var typeName = reader.Reader.ReadString();
                        expectedType = Type.GetType(typeName);
                        break;
                    case DataKind.Assembly:

                        break;
                    case DataKind.Graph:
                        return ReadGraph(reader, expectedType);
                    default:
                        throw new SerializationException("Data is invalid");
                }
            }
        }

        private static object ReadGraph(DeserializerSession reader, Type expectedType)
        {
            var serializer = GetSerializer(expectedType);
            return serializer.ReadObject(reader);
        }

        public Stream Serialize<T>(T graph)
        {
            MemoryStream ms = new MemoryStream();
            var writer = new SerializerSession(ms);
            writer.Writer.Write(MagicHeader);
            Serialize(writer, graph, typeof(T));
            ms.Position = 0;
            return ms;
        }

        internal static void Serialize(SerializerSession bw, object graph, Type expectedType)
        {
            if (graph is null)
            {
                bw.Stream.WriteByte((byte)DataKind.Null);
                return;
            }

            var actualType = graph.GetType();
            if (actualType != expectedType)
            {
                bw.WriteType(actualType);
            }

            bw.Stream.WriteByte((byte)DataKind.Graph);

            var serializer = GetSerializer(actualType);
            serializer.WriteObject(bw, graph);
        }

        private static Dictionary<Type, ITypeSerializer> s_knownSerializers;

        internal class SimpleTypeSerializer : ITypeSerializer
        {
            private readonly Action<SerializerSession, object> m_write;
            private readonly Func<DeserializerSession, object> m_read;

            public SimpleTypeSerializer(Action<SerializerSession, object> write, Func<DeserializerSession, object> read)
            {
                m_write = write;
                m_read = read;
            }

            public object ReadObject(DeserializerSession reader)
            {
                return m_read(reader);
            }

            public void WriteObject(SerializerSession bw, object graph)
            {
                m_write(bw, graph);
            }
        }

        static DefaultBinarySerializer()
        {
            s_knownSerializers = new Dictionary<Type, ITypeSerializer>();

            void AddSerializer(Type t, ITypeSerializer s)
            {
                s_knownSerializers.Add(t, s);
            }

            AddSerializer(typeof(int), new SimpleTypeSerializer(
                (bw, o) => bw.Writer.Write((int)o),
                br => BoxHelper.Box(br.Reader.ReadInt32())));
            AddSerializer(typeof(long), new SimpleTypeSerializer(
                (bw, o) => bw.Writer.Write((long)o),
                br => BoxHelper.Box(br.Reader.ReadInt64())));
            AddSerializer(typeof(DateTime), new SimpleTypeSerializer(
                (bw, o) => bw.Writer.Write(((DateTime)o).ToUniversalTime().Ticks),
                br => BoxHelper.Box(new DateTime(br.Reader.ReadInt64(), DateTimeKind.Utc))));
            AddSerializer(typeof(TimeSpan), new SimpleTypeSerializer(
                (bw, o) => bw.Writer.Write(((TimeSpan)o).Ticks),
                br => BoxHelper.Box(new TimeSpan(br.Reader.ReadInt64()))));
            AddSerializer(typeof(string), new SimpleTypeSerializer(
                (bw, o) => bw.Writer.Write((string)o),
                br => br.Reader.ReadString()));
            AddSerializer(typeof(CancellationToken), new SimpleTypeSerializer(
                (bw, o) => { },
                br => BoxHelper.BoxedCancellationToken));
        }

        internal static ITypeSerializer GetSerializer(Type actualType)
        {
            lock (s_knownSerializers)
            {
                if (s_knownSerializers.TryGetValue(actualType, out var s))
                    return s;
            }

            var serializer = CreateSerializer(actualType);
            lock (s_knownSerializers)
            {
                s_knownSerializers[actualType] = serializer;
            }

            return serializer;
        }

        private static ITypeSerializer CreateSerializer(Type actualType)
        {
            if (actualType.GetCustomAttribute<DataContractAttribute>() != null)
            {
                return new ReflectedDataContractSerializer(actualType);
            }

            if (actualType.IsArray)
            {
                return ArraySerializer.Create(actualType.GetElementType());
            }

            throw new InvalidOperationException("Unable to serialize type " + actualType);
        }
    }
}
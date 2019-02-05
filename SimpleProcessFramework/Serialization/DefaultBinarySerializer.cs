using Spfx.Reflection;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

namespace Spfx.Serialization
{
    internal class DefaultBinarySerializer : IBinarySerializer
    {
        internal const int MagicHeader = unchecked((int)0xBEEF1234);

        public T Deserialize<T>(Stream s)
        {
            var reader = new DeserializerSession(s);

            reader.PrepareRead();

            return (T)Deserialize(reader, ReflectionUtilities.GetType<T>());
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
                        var typeName = (ReflectedTypeInfo)reader.ReadReference(readHeader: true);
                        expectedType = typeName.ResolvedType;
                        break;
                    case DataKind.Assembly:

                        break;
                    case DataKind.Graph:
                        return DeserializeExactType(reader, expectedType);
                    case DataKind.Ref:
                        return reader.ReadReference(readHeader: false);
                    default:
                        throw new SerializationException("Data is invalid");
                }
            }
        }

        internal static object DeserializeExactType(DeserializerSession reader, Type expectedType)
        {
            var serializer = GetSerializer(expectedType);
            return serializer.ReadObject(reader);
        }

        public byte[] SerializeToBytes<T>(T graph, bool lengthPrefix)
        {
            var ms = (MemoryStream)Serialize(graph, lengthPrefix);
            return ms.ToArray();
        }

        public Stream Serialize<T>(T graph, bool lengthPrefix, int startOffset = 0)
        {
            MemoryStream ms = new MemoryStream();

            ms.Position = startOffset + (lengthPrefix ? 4 : 0);

            var writer = new SerializerSession(ms);
            writer.BeginSerialization();
            Serialize(writer, graph, ReflectionUtilities.GetType<T>());
            writer.FinishSerialization();

            if (lengthPrefix)
                writer.WritePositionDelta(startOffset);

            ms.Position = startOffset;

            return ms;
        }

        internal static void Serialize(SerializerSession bw, object graph, Type expectedType)
        {
            if (graph is null)
            {
                bw.WriteMetadata(DataKind.Null);
                return;
            }

            var actualType = graph.GetType();
            if (actualType != expectedType)
            {
                bw.WriteType(actualType);
            }

            bw.WriteMetadata(DataKind.Graph);

            SerializeExactType(bw, graph, actualType);
        }

        internal static void SerializeExactType(SerializerSession bw, object graph, Type actualType)
        {
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

            void AddSerializer2<T>(Action<SerializerSession, T> serializer, Func<DeserializerSession,T> deserializer)
            {
                AddSerializer(typeof(T), new SimpleTypeSerializer(
                    (bw, o) => serializer(bw, (T)o),
                    br => BoxHelper.Box(deserializer(br))));
            }

            AddSerializer2((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadByte());
            AddSerializer2((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadInt32());
            AddSerializer2((bw, o) => bw.Writer.Write(o),br => br.Reader.ReadInt64());
            AddSerializer2((bw, o) => bw.Writer.Write(o.ToUniversalTime().Ticks), br => new DateTime(br.Reader.ReadInt64(), DateTimeKind.Utc));
            AddSerializer2((bw, o) => bw.Writer.Write(o.Ticks), br => new TimeSpan(br.Reader.ReadInt64()));
            AddSerializer2((bw, o) => bw.Writer.Write(o),br => br.Reader.ReadString());
            AddSerializer2((bw, o) => bw.Writer.Write(o ? (byte)1 : (byte)0),br => br.Reader.ReadByte() != 0);
            AddSerializer2((bw, o) => { }, br => CancellationToken.None);
            AddSerializer2((bw, o) => { },br => EventArgs.Empty);
            AddSerializer2(WriteIPAddres, ReadIPAddress);
        }

        private static void WriteIPAddres(SerializerSession session, IPAddress val)
        {
            session.Writer.Write((byte)val.AddressFamily);
            session.Writer.Write(val.GetAddressBytes());
        }

        private static IPAddress ReadIPAddress(DeserializerSession session)
        {
            var af = (AddressFamily)session.Reader.ReadByte();
            if (af == AddressFamily.InterNetwork)
                return new IPAddress(session.Reader.ReadBytes(4));
            if (af == AddressFamily.InterNetworkV6)
                return new IPAddress(session.Reader.ReadBytes(16));
            throw new SerializationException("Only IPv4 and IPv6 are supported");
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

            if(actualType.IsEnum)
            {
                return EnumSerializer.Create(actualType);
            }

            if (actualType.IsGenericType)
            {
                var baseType = actualType.GetGenericTypeDefinition();
                var genericArgs = actualType.GetGenericArguments();
                if (baseType == typeof(List<>))
                {
                    return ListSerializer.Create(genericArgs.Single());
                }
            }

            throw new InvalidOperationException("Unable to serialize type " + actualType);
        }
    }
}
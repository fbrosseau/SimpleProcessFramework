using Spfx.Reflection;
using Spfx.Serialization.DataContracts;
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

        private static Dictionary<Type, Type> s_typeSubstitutions;

        public T Deserialize<T>(Stream s)
        {
            var reader = new DeserializerSession(s);

            reader.PrepareRead();

            var res = (T)Deserialize(reader, ReflectionUtilities.GetType<T>());

            reader.EndRead();

            return res;
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
            Type replacement = null;
            if (s_typeSubstitutions?.TryGetValue(actualType, out replacement) == true)
                actualType = replacement;

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

        private static readonly Dictionary<Type, ITypeSerializer> s_knownSerializers;

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

            void AddGeneric<T>(Action<SerializerSession, T> serializer, Func<DeserializerSession, T> deserializer, bool discoverAllImplementations = false)
            {
                var thisT = typeof(T);
                var serializerInstance = new SimpleTypeSerializer(
                    (bw, o) => serializer(bw, (T)o),
                    br => BoxHelper.Box(deserializer(br)));

                AddSerializer(thisT, serializerInstance);

                if (discoverAllImplementations)
                {
                    foreach (var t in thisT.Assembly.DefinedTypes)
                    {
                        if (t != thisT && thisT.IsAssignableFrom(t))
                        {
                            AddSerializer(t, serializerInstance);
                            if (s_typeSubstitutions is null)
                                s_typeSubstitutions = new Dictionary<Type, Type>();
                            s_typeSubstitutions[t] = thisT;
                        }
                    }
                }
            }

            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadByte());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadSByte());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadInt16());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadUInt16());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadInt32());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadUInt32());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadInt64());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadUInt64());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadSingle());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadDouble());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadDecimal());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadChar());
            AddGeneric((bw, o) => bw.Writer.Write(o.ToUniversalTime().Ticks), br => new DateTime(br.Reader.ReadInt64(), DateTimeKind.Utc));
            AddGeneric((bw, o) => bw.Writer.Write(o.Ticks), br => new TimeSpan(br.Reader.ReadInt64()));
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadString());
            AddGeneric((bw, o) => bw.Writer.Write(o ? (byte)1 : (byte)0), br => br.Reader.ReadByte() != 0);
            AddGeneric((bw, o) => { }, br => CancellationToken.None);
            AddGeneric((bw, o) => { }, br => EventArgs.Empty);
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadGuid());
            AddGeneric(WriteIPAddress, ReadIPAddress, discoverAllImplementations: true);
            AddGeneric(WriteVersion, ReadVersion);
        }

        private static void WriteVersion(SerializerSession session, Version val)
        {
            session.Writer.Write(val.ToString());
        }

        private static Version ReadVersion(DeserializerSession session)
        {
            return new Version(session.Reader.ReadString());
        }

        private static unsafe void WriteIPAddress(SerializerSession session, IPAddress val)
        {
            session.Writer.Write((byte)val.AddressFamily);

#if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
            Span<byte> buf = stackalloc byte[16];
            val.TryWriteBytes(buf, out var count);
            session.Writer.Write(buf.Slice(0, count));
#else
            session.Writer.Write(val.GetAddressBytes());
#endif
        }

        private static ReadOnlySpan<byte> s_sixteenZeroBytes => new byte[16];
        private static ReadOnlySpan<byte> s_ipv6LoopbackBytes => new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
        private static ReadOnlySpan<byte> s_ipv4To6LoopbackBytes => new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 127, 0, 0, 1 };
        private static readonly IPAddress s_ipv4To6 = new IPAddress(s_ipv4To6LoopbackBytes.ToArray());

        private static unsafe IPAddress ReadIPAddress(DeserializerSession session)
        {
            var af = (AddressFamily)session.Reader.ReadByte();
            if (af == AddressFamily.InterNetwork)
            {
                var ipInt = session.Reader.ReadUInt32();
                switch (ipInt)
                {
                    case 0:
                        return IPAddress.Any;
                    case 0x0100007F:
                        return IPAddress.Loopback;
                    case 0xFFFFFFFF:
                        return IPAddress.Broadcast;
                    default:
                        return new IPAddress(ipInt);
                }
            }

            if (af != AddressFamily.InterNetworkV6)
                throw new InvalidOperationException("The endpoint must be IPv4 or IPv6");

#if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
            Span<byte> span = stackalloc byte[16];
            session.Reader.ReadAll(span);
#else
            var bytes = session.Reader.ReadBytes(16);
            var span = new ReadOnlySpan<byte>(bytes);
#endif
            switch(span[15])
            {
                case 1:
                    if (span.SequenceEqual(s_ipv6LoopbackBytes))
                        return IPAddress.IPv6Loopback;
                    if (span.SequenceEqual(s_ipv4To6LoopbackBytes))
                        return s_ipv4To6;
                    break;
                case 0:
                    if (span.SequenceEqual(s_sixteenZeroBytes))
                        return IPAddress.IPv6None;
                    break;
            }

#if NETSTANDARD2_1_PLUS || NETCOREAPP2_1_PLUS
            return new IPAddress(span);
#else
            return new IPAddress(bytes);
#endif
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
                return BaseReflectedDataContractSerializer.Create(actualType);
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
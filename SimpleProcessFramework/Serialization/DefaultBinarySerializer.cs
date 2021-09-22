using Spfx.Diagnostics.Logging;
using Spfx.Io;
using Spfx.Reflection;
using Spfx.Serialization.DataContracts;
using Spfx.Serialization.Serializers;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Spfx.Serialization
{
    internal class DefaultBinarySerializer : IBinarySerializer
    {
        internal const int MagicHeader = unchecked((int)0xBEEF1234);

        private static Dictionary<Type, Type> s_typeSubstitutions;
        private readonly ILogger m_logger;

        public DefaultBinarySerializer(ITypeResolver resolver)
        {
            m_logger = resolver.GetLogger(GetType());
        }

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
                        throw ThrowBadSerializationException("Data is invalid");
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
            using var ms = Serialize(graph, lengthPrefix);
            return StreamExtensions.CopyToByteArray(ms);
        }

        public Stream Serialize<T>(T graph, bool lengthPrefix, int startOffset = 0)
        {
            // 512 was guesstimated to be the average message size in use by spfx
            // when running all unit tests and looking at avg/median.
            var ms = new RentedMemoryStream(512);

            try
            {
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
            catch (Exception ex)
            {
                ms.Dispose();
                m_logger.Error?.Trace(ex, "Serialization failed: " + ex.Message);
                throw;
            }
        }

        public T DeepClone<T>(T obj)
        {
            using var stream = Serialize(obj, false);
            return Deserialize<T>(stream);
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

            GetSerializer(actualType).WriteObjectWithHeader(bw, graph);
        }

        internal static void SerializeExactType(SerializerSession bw, object graph, Type actualType)
        {
            var serializer = GetSerializer(actualType);
            serializer.WriteObject(bw, graph);
        }

        private static readonly Dictionary<Type, ITypeSerializer> s_knownSerializers;

#pragma warning disable CA1810 // explicit static cctor
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
                var serializerInstance = new SimpleTypeSerializer<T>(
                    serializer,
                    deserializer);

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
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadGuid());
            AddGeneric((bw, o) => bw.Writer.Write(o), br => br.Reader.ReadString());
            AddGeneric((bw, o) => { }, br => CancellationToken.None);
            AddGeneric((bw, o) => { }, br => EventArgs.Empty);
            AddGeneric(WriteIPAddress, ReadIPAddress, discoverAllImplementations: true);
            AddGeneric(WriteVersion, ReadVersion);
            AddGeneric(WriteCertificate, ReadCertificate);
            AddGeneric(WriteCertificate2, ReadCertificate2);

            AddSerializer(typeof(bool), BoolSerializer.Serializer);
            AddSerializer(typeof(bool?), BoolSerializer.NullableSerializer);
        }
#pragma warning restore CA1810

        private static void WriteCertificate2(SerializerSession session, X509Certificate2 cert) => WriteCertificate(session, cert);
        private static X509Certificate ReadCertificate(DeserializerSession session) => ReadCertificate2(session);

        private static X509Certificate2 ReadCertificate2(DeserializerSession session)
        {
            var len = checked((int)session.Reader.ReadEncodedUInt32());
            return new X509Certificate2(session.Reader.ReadBytes(len));
        }

        private static void WriteCertificate(SerializerSession session, X509Certificate cert)
        {
            var bytes = cert.GetRawCertData();
            session.Writer.WriteEncodedUInt32((uint)bytes.Length);
            session.Writer.Write(bytes);
        }

        private static void WriteVersion(SerializerSession session, Version val)
        {
            session.Writer.Write(val.ToString());
        }

        private static Version ReadVersion(DeserializerSession session)
        {
            return new Version(session.Reader.ReadString());
        }

        private static void WriteIPAddress(SerializerSession session, IPAddress val)
        {
            session.Writer.Write((byte)val.AddressFamily);

#if NETSTANDARD2_1_PLUS || NETCOREAPP
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

        private static IPAddress ReadIPAddress(DeserializerSession session)
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
                ThrowBadSerializationException("The endpoint must be IPv4 or IPv6");

#if NETSTANDARD2_1_PLUS || NETCOREAPP
            Span<byte> span = stackalloc byte[16];
            session.Reader.ReadAll(span);
#else
            var bytes = session.Reader.ReadBytes(16);
            var span = new ReadOnlySpan<byte>(bytes);
#endif
            switch (span[15])
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

#if NETSTANDARD2_1_PLUS || NETCOREAPP
            return new IPAddress(span);
#else
            return new IPAddress(bytes);
#endif
        }

        internal static ITypeSerializer<T> GetSerializer<T>()
        {
            var serializer = GetSerializer(ReflectionUtilities.GetType<T>());
            if (serializer is ITypeSerializer<T> typed)
                return typed;
            return new TypedSerializerWrapper<T>(serializer);
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

            if (actualType.IsEnum)
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
                if (baseType == typeof(Nullable<>))
                {
                    return NullableSerializer.Create(genericArgs.Single());
                }
            }

            throw ThrowBadSerializationException("Unable to serialize type " + actualType);
        }

        internal static Exception ThrowBadSerializationException(string msg = null)
        {
            throw new SerializationException(msg ?? "Serialization failed!");
        }
    }
}
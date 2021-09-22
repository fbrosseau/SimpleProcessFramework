using Spfx.Utilities;
using Spfx.Utilities.ApiGlue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Serialization.Serializers
{
    internal static class EnumSerializer
    {
        public static ITypeSerializer Create(Type enumType)
        {
            return (ITypeSerializer)Activator.CreateInstance(typeof(EnumSerializer<>).MakeGenericType(enumType));
        }
    }

    internal sealed unsafe class EnumSerializer<TEnum> : TypedSerializer<TEnum>
        where TEnum : unmanaged, Enum
    {
        private readonly Dictionary<TEnum, object> m_boxedValues = new Dictionary<TEnum, object>();

        public EnumSerializer()
        {
            var vals = ((TEnum[])Enum
                .GetValues(TypeofT))
                .Distinct()
                .Where(v => !EqualityComparer<TEnum>.Default.Equals(v, default))
                .ToArray();

            if (TypeofT.GetCustomAttribute<FlagsAttribute>() != null)
            {
                if(vals.Length <= 5)
                {
                    vals = vals
                        .Select(EnumToNumber)
                        .ToArray()
                        .GenerateCombinations()
                        .Select(comb => NumberToEnum(comb.Aggregate((a, b) => a | b)))
                        .ToArray();
                }
            }

            if (vals.Length <= 32) // completely arbitrary
            {
                foreach (var val in vals)
                {
                    m_boxedValues[val] = val;
                }
            }

            m_boxedValues[default] = default(TEnum);
        }

        public sealed override object ReadObject(DeserializerSession session)
        {
            var e = ReadTypedObject(session);
            if (m_boxedValues.TryGetValue(e, out var boxed))
                return boxed;
            return e;
        }

        public sealed override TEnum ReadTypedObject(DeserializerSession session)
        {
            return NumberToEnum((sizeof(TEnum)) switch
            {
                1 => session.Reader.ReadByte(),
                2 => session.Reader.ReadUInt16(),
                4 => session.Reader.ReadUInt32(),
                8 => session.Reader.ReadUInt64(),
                _ => throw DefaultBinarySerializer.ThrowBadSerializationException("Invalid enum size")
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TEnum NumberToEnum(ulong val)
        {
            return (sizeof(TEnum)) switch
            {
                1 => UnsafeGlue.UnmanagedAs<byte, TEnum>((byte)val),
                2 => UnsafeGlue.UnmanagedAs<ushort, TEnum>((ushort)val),
                4 => UnsafeGlue.UnmanagedAs<uint, TEnum>((uint)val),
                8 => UnsafeGlue.UnmanagedAs<ulong, TEnum>(val),
                _ => throw DefaultBinarySerializer.ThrowBadSerializationException("Invalid enum size")
            };
        }

        private ulong EnumToNumber(TEnum val)
        {
            return (sizeof(TEnum)) switch
            {
                1 => UnsafeGlue.UnmanagedAs<TEnum, byte>(val),
                2 => UnsafeGlue.UnmanagedAs<TEnum, ushort>(val),
                4 => UnsafeGlue.UnmanagedAs<TEnum, uint>(val),
                8 => UnsafeGlue.UnmanagedAs<TEnum, ulong>(val),
                _ => throw DefaultBinarySerializer.ThrowBadSerializationException("Invalid enum size"),
            };
        }

        public sealed override void WriteObject(SerializerSession session, object graph)
        {
            WriteObject(session, (TEnum)graph);
        }

        public sealed override void WriteObject(SerializerSession session, TEnum graph)
        {
            switch (sizeof(TEnum))
            {
                case 1:
                    session.Writer.Write((byte)EnumToNumber(graph));
                    break;
                case 2:
                    session.Writer.Write((ushort)EnumToNumber(graph));
                    break;
                case 4:
                    session.Writer.Write((uint)EnumToNumber(graph));
                    break;
                case 8:
                    session.Writer.Write(EnumToNumber(graph));
                    break;
                default:
                    throw DefaultBinarySerializer.ThrowBadSerializationException("Invalid enum size");
            }
        }
    }
}
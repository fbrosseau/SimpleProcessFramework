using NUnit.Framework;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Tests
{
    public class DisposableObject : Disposable
    {
    }

    [DataContract]
    public class TestContract : IEquatable<TestContract>
    {
        [DataMember]
        public object Value { get; set; }

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override bool Equals(object obj) => Equals(obj as TestContract);
        public bool Equals(TestContract other)
        {
            return other != null && Equals(Value, other.Value);
        }

        public override string ToString()
        {
            return GetType().Name + ":" + Value?.ToString() ?? "<null>";
        }
    }

    [DataContract]
    public class GenericTestContract<T> : IEquatable<GenericTestContract<T>>
    {
        [DataMember]
        public T Value { get; set; }

        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value);
        public override bool Equals(object obj) => Equals(obj as GenericTestContract<T>);
        public bool Equals(GenericTestContract<T> other)
        {
            return other?.GetType() == GetType() && EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override string ToString()
        {
            return GetType().Name + ":" + Value?.ToString() ?? "<null>";
        }
    }

    [DataContract]
    public class InheritedContract : GenericTestContract<int>
    {
        [DataMember]
        public int Value2 { get; set; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj)
                && ((InheritedContract)obj).Value2 == Value2;
        }

        public override int GetHashCode() => base.GetHashCode();
    }

    public enum ByteEnum : byte { A = 5, B, C }
    public enum IntEnum { A = int.MinValue, B, C }
    public enum LongEnum : long { A = -200000000, B, C }
    public enum ULongEnum : ulong { A = 0x9999999, B, C }

    [Flags]
    public enum FlagsEnum { A = 1, B = 2, C = 4 }

    [Flags]
    public enum BigFlagsEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128, I = 256, J = 512, K = 1024, L = 2048, M = 4096, N = 8192, O = 16384, P = 32768, Q = 65536, R = 131072, S = 262144, T = 524288, U = 1048576, V = 2097152, W = 4194304, X = 8388608, Y = 16777216, Z = 33554432, AA = 67108864, AB = 134217728, AC = 268435456, AD = 536870912, AE = 1073741824, AF = unchecked((int)2147483648) }

    public class CustomException_NotMarshalled : Exception
    {
        public CustomException_NotMarshalled(string msg)
            : base(msg)
        {
        }
    }

    [DataContract]
    public class CustomException_Marshalled : SerializableException
    {
        [DataMember]
        public string PropertyValue { get; set; }

        public CustomException_Marshalled(string msg)
            : base(msg)
        {
            PropertyValue = CreatePropertyValue(msg);
        }

        public static string CreatePropertyValue(string msg)
        {
            return "!!!!!" + msg + "!!!!!";
        }

        internal void AssertValues(string text)
        {
            Assert.AreEqual(text, Message);
            Assert.AreEqual(CreatePropertyValue(text), PropertyValue);
        }
    }
}
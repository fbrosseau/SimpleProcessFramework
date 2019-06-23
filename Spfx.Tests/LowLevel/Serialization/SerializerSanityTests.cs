using NUnit.Framework;
using Spfx.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Spfx.Utilities;

namespace Spfx.Tests.LowLevel.Serialization
{
    [TestFixture, Parallelizable]
    public class BasicSerializerSanityTests : CommonTestClass
    {
        [Test, TestCaseSource(nameof(GenerateSampleObjects))]
        public void BasicSerializerSanityTests_Simple(Type t, object value)
        {
            var serializer = new DefaultBinarySerializer();
            var bytes = serializer.Serialize(value, false);
            bytes.Position = 0;
            long len = bytes.Length;
            var deserialized = serializer.Deserialize<object>(bytes);
            Assert.AreEqual(len, bytes.Position, "Not all bytes were read when serializing " + t.FullName);

            CompareEquality(value, deserialized);
        }

        private void CompareEquality(object value, object deserialized)
        {
            if (value is ICollection a && deserialized is ICollection b)
            {
                Assert.AreEqual(a.Count, b.Count, "Count not equal");
                foreach (var (i1, i2) in a.Cast<object>().Zip(b.Cast<object>()))
                {
                    CompareEquality(i1, i2);
                }
            }
            else
            {
                Assert.AreEqual(value, deserialized);
            }
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
                return Value?.ToString() ?? "<null>";
            }
        }

        private static object[][] GenerateSampleObjects()
        {
            var values = new object[]
            {
                (sbyte)5,
                (short)1234,
                449292924,
                3496093260920L,
                (byte)53,
                (ushort)1234,
                449292924u,
                3496093260920UL,
                0.5f,
                0.5,
                (decimal)0.5,
                new Guid("11112222-3333-4444-5555-666677778888"),
                new TestContract(),
                new TestContract{ Value = "Value" },
                new TestContract{ Value = new TestContract() },
            };

            var arrays = values.Select(o =>
            {
                var t = o.GetType();
                var arr = Array.CreateInstance(t, 3);
                arr.SetValue(o, 0);
                arr.SetValue(o, 1);
                // leave index 2
                return arr;
            });

            IList MakeList(Type t)
            {
                return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t));
            }

            var lists = values.Select(o =>
            {
                var t = o.GetType();
                var list = MakeList(o.GetType());
                list.Add(o);
                return list;
            });

            var emptyLists = values.Select(o =>
            {
                return MakeList(o.GetType());
            });

            return values.Concat(arrays).Concat(lists).Concat(emptyLists).Select(o => new object[] { o.GetType(), o }).ToArray();
        }
    }
}

using NUnit.Framework;
using Spfx.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spfx.Utilities;
using System.Net;
using Spfx.Reflection;

namespace Spfx.Tests.LowLevel.Serialization
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class BasicSerializerSanityTests : CommonTestClass
    {
        [Test, TestCaseSource(nameof(GenerateSampleObjects))]
        public void BasicSerializerSanityTests_Simple(Type t, object value)
        {
            var serializer = new DefaultBinarySerializer(DefaultTestResolver);
            using var bytes = serializer.Serialize(value, false);
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
                if (a.Count == 0 && a.GetType().IsArray)
                {
                    Assert.AreSame(a, b, "Not deserialized as Array.Empty");
                }
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

        private static object[][] GenerateSampleObjects()
        {
            (object Value, Type Type) CreateTypedObject(object o, Type t = null)
            {
                return (o, t ?? o?.GetType());
            }

            var values = new object[]
            {
                true,
                false,
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
                new IPAddress(new byte[]{1,2,3,4}),
                new IPAddress(new byte[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16}),
                IPAddress.Loopback,
                IPAddress.IPv6Loopback,
                Array.Empty<string>(),
                ByteEnum.B,
                IntEnum.B,
                LongEnum.B,
                ULongEnum.B,
                FlagsEnum.A | FlagsEnum.C,
                BigFlagsEnum.A | BigFlagsEnum.AF,
                (ReflectedTypeInfo)typeof(string),
                (ReflectedTypeInfo)typeof(FlagsEnum)
            }.Select(o => CreateTypedObject(o)).ToArray();

            var valueTypes = values.Where(o => o.Type.IsValueType).ToArray();
            var nullables = valueTypes
                .Select(v => MakeNullable(v.Value, v.Type))
                .Concat(valueTypes.Select(v => MakeNullable(null, v.Type)));

            values = values.Concat(nullables).ToArray();

            var arrays = values.Select(o =>
            {
                var arr = Array.CreateInstance(o.Type, 3);
                arr.SetValue(o.Value, 0);
                arr.SetValue(o.Value, 1);
                // leave index 2
                return CreateTypedObject(arr);
            });

            IList MakeList(Type t)
            {
                return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t));
            }

            var lists = values.Select(o =>
            {
                var list = MakeList(o.Type);
                list.Add(o.Value);
                return CreateTypedObject(list);
            });

            var emptyLists = values.Select(o =>
            {
                return CreateTypedObject(MakeList(o.Type));
            });

            return values.Concat(arrays).Concat(lists).Concat(emptyLists).Select(o => new[] { o.Type, o.Value }).ToArray();
        }

        private static (object Value, Type Type) MakeNullable(object value, Type type)
        {
            var nullableT = typeof(Nullable<>).MakeGenericType(type);
            return (value, nullableT);
        }
    }
}

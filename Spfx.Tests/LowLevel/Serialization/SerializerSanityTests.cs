using NUnit.Framework;
using Spfx.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spfx.Utilities;
using System.Net;
using Spfx.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Spfx.Tests.LowLevel.Serialization
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class BasicSerializerSanityTests : CommonTestClass
    {
        private static readonly X509Certificate2 s_sampleCertificate = new X509Certificate2(new byte[] { 48, 130, 3, 21, 48, 130, 1, 253, 160, 3, 2, 1, 2, 2, 16, 21, 228, 112, 222, 25, 14, 138, 190, 73, 89, 77, 58, 41, 229, 125, 195, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0, 48, 19, 49, 17, 48, 15, 6, 3, 85, 4, 3, 12, 8, 84, 101, 115, 116, 67, 101, 114, 116, 48, 30, 23, 13, 50, 48, 48, 52, 49, 48, 49, 55, 51, 57, 51, 50, 90, 23, 13, 50, 49, 48, 52, 49, 48, 49, 55, 53, 57, 51, 50, 90, 48, 19, 49, 17, 48, 15, 6, 3, 85, 4, 3, 12, 8, 84, 101, 115, 116, 67, 101, 114, 116, 48, 130, 1, 34, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 1, 5, 0, 3, 130, 1, 15, 0, 48, 130, 1, 10, 2, 130, 1, 1, 0, 202, 74, 39, 165, 185, 173, 220, 20, 58, 252, 18, 66, 23, 197, 200, 196, 128, 243, 129, 78, 194, 247, 4, 130, 124, 73, 98, 3, 169, 109, 56, 26, 43, 2, 116, 39, 115, 148, 152, 126, 154, 101, 80, 187, 25, 212, 221, 207, 28, 121, 60, 98, 186, 67, 234, 65, 86, 136, 253, 55, 38, 195, 126, 154, 54, 223, 171, 19, 79, 3, 77, 168, 77, 102, 137, 170, 24, 28, 79, 202, 61, 35, 34, 56, 194, 214, 59, 247, 134, 218, 105, 71, 60, 161, 158, 21, 181, 23, 70, 185, 72, 208, 235, 210, 79, 41, 163, 105, 47, 165, 217, 36, 119, 145, 151, 245, 186, 117, 229, 58, 108, 40, 14, 80, 181, 95, 150, 188, 115, 121, 235, 38, 173, 67, 233, 172, 98, 202, 210, 249, 65, 239, 177, 168, 49, 194, 167, 70, 156, 152, 222, 75, 14, 240, 224, 162, 93, 220, 114, 194, 128, 242, 145, 103, 147, 216, 140, 8, 138, 115, 7, 14, 74, 192, 64, 254, 82, 134, 165, 73, 145, 181, 216, 82, 161, 78, 113, 123, 111, 0, 123, 148, 183, 113, 35, 79, 254, 152, 23, 206, 60, 76, 128, 103, 221, 130, 108, 78, 43, 63, 4, 229, 226, 60, 143, 77, 20, 93, 182, 42, 207, 147, 33, 239, 33, 59, 149, 144, 165, 229, 10, 164, 40, 147, 77, 144, 52, 5, 228, 136, 197, 30, 221, 191, 44, 159, 55, 255, 255, 217, 157, 125, 12, 208, 251, 193, 2, 3, 1, 0, 1, 163, 101, 48, 99, 48, 14, 6, 3, 85, 29, 15, 1, 1, 255, 4, 4, 3, 2, 5, 160, 48, 29, 6, 3, 85, 29, 37, 4, 22, 48, 20, 6, 8, 43, 6, 1, 5, 5, 7, 3, 2, 6, 8, 43, 6, 1, 5, 5, 7, 3, 1, 48, 19, 6, 3, 85, 29, 17, 4, 12, 48, 10, 130, 8, 84, 101, 115, 116, 67, 101, 114, 116, 48, 29, 6, 3, 85, 29, 14, 4, 22, 4, 20, 241, 193, 39, 161, 8, 216, 112, 206, 253, 247, 219, 254, 161, 212, 8, 191, 241, 173, 22, 217, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0, 3, 130, 1, 1, 0, 98, 3, 132, 99, 48, 163, 187, 118, 45, 26, 120, 16, 43, 193, 94, 64, 76, 244, 102, 108, 188, 237, 122, 68, 186, 101, 17, 115, 17, 53, 181, 40, 75, 22, 196, 54, 17, 228, 137, 197, 79, 42, 124, 29, 221, 79, 59, 139, 224, 196, 21, 84, 35, 57, 223, 130, 20, 165, 187, 75, 78, 78, 121, 197, 229, 4, 163, 42, 37, 250, 158, 103, 143, 176, 236, 162, 157, 197, 85, 93, 209, 0, 106, 224, 193, 138, 70, 185, 114, 13, 137, 134, 236, 198, 134, 77, 169, 5, 3, 206, 63, 212, 244, 201, 183, 164, 199, 66, 133, 75, 149, 240, 14, 194, 1, 61, 59, 122, 118, 122, 30, 18, 187, 14, 74, 99, 144, 62, 16, 33, 56, 71, 7, 95, 105, 127, 156, 169, 98, 35, 110, 127, 141, 14, 112, 13, 76, 132, 135, 96, 97, 89, 54, 216, 161, 217, 125, 208, 201, 240, 39, 128, 132, 210, 6, 247, 165, 181, 99, 242, 183, 111, 194, 160, 123, 131, 21, 139, 9, 123, 115, 235, 20, 115, 183, 62, 161, 64, 165, 4, 59, 55, 87, 57, 42, 196, 72, 65, 41, 175, 60, 115, 132, 60, 28, 78, 200, 38, 134, 126, 189, 65, 163, 128, 18, 160, 252, 180, 246, 185, 162, 5, 56, 36, 25, 202, 111, 59, 134, 12, 83, 74, 97, 165, 105, 84, 76, 231, 76, 65, 200, 164, 195, 77, 107, 51, 30, 233, 223, 166, 162, 0, 65, 138, 106, 151 });

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
                new TestContract{ Value = 5 },
                new GenericTestContract<int> { Value = 5 },
                new GenericTestContract<int?> { Value = 5 },
                new GenericTestContract<string> { Value = "asdf" },
                new InheritedContract { Value = 5, Value2 = 15 },
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
                (ReflectedTypeInfo)typeof(FlagsEnum),
                s_sampleCertificate
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

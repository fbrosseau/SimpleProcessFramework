using NUnit.Framework;
using System.Runtime.Serialization;

namespace Spfx.Tests
{
    [DataContract]
    public class TestReturnValue
    {
        public const string ExpectedDummyValue = "allo";

        [DataMember]
        public string DummyValue { get; set; } // not setting default value so that it doesn't unknowingly cheat

        public static TestReturnValue Create()
        {
            return new TestReturnValue { DummyValue = ExpectedDummyValue };
        }

        internal static void Verify(TestReturnValue res)
        {
            Assert.AreEqual(ExpectedDummyValue, res.DummyValue);
        }
    }
}

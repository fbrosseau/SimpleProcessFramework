using NUnit.Framework;
using System.Runtime.Serialization;

namespace Spfx.Tests
{
    [DataContract]
    public class DummyReturn
    {
        public const string ExpectedDummyValue = "allo";

        [DataMember]
        public string DummyValue { get; set; } // not setting default value so that it doesn't unknowingly cheat

        public static DummyReturn Create()
        {
            return new DummyReturn { DummyValue = ExpectedDummyValue };
        }

        internal static void Verify(DummyReturn res)
        {
            Assert.AreEqual(ExpectedDummyValue, res.DummyValue);
        }
    }
}

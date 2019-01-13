using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    [DataContract]
    public class IpcHandshakeRequest : IIpcFrame
    {
        public const int ExpectedMagicNumber = 0x11223344;

        [DataMember]
        public int MagicNumber { get; set; }
    }
}
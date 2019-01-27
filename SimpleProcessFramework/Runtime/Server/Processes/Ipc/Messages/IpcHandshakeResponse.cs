using System.Runtime.Serialization;

namespace Spfx.Runtime.Server.Processes
{
    [DataContract]
    public class IpcHandshakeResponse : IIpcFrame
    {
        public const int ExpectedMagicNumber = 0x44332211;

        [DataMember]
        public int MagicNumber { get; set; }
    }
}

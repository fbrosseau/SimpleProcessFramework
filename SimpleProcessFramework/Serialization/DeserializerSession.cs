using System.IO;

namespace SimpleProcessFramework.Serialization
{
    internal class DeserializerSession
    {
        public Stream Stream { get; }
        public BinaryReader Reader { get; }

        public DeserializerSession(Stream s)
        {
            Stream = s;
            Reader = new BinaryReader(s);
        }
    }
}
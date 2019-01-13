using System.IO;

namespace SimpleProcessFramework.Serialization
{
    public interface IBinarySerializer
    {
        Stream Serialize<T>(T graph, bool lengthPrefix, int startOffset = 0);
        byte[] SerializeToBytes<T>(T msg, bool lengthPrefix);

        T Deserialize<T>(Stream s);
    }
}
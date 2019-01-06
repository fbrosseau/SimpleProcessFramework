using System.IO;

namespace SimpleProcessFramework.Serialization
{
    public interface IBinarySerializer
    {
        Stream Serialize<T>(T graph, bool lengthPrefix);
        T Deserialize<T>(Stream s);
    }
}
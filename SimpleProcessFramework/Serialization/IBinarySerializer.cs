using System.IO;

namespace SimpleProcessFramework.Serialization
{
    internal interface IBinarySerializer
    {
        Stream Serialize<T>(T graph);
        T Deserialize<T>(Stream s);
    }
}
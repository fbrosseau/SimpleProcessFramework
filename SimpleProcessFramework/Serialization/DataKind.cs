namespace SimpleProcessFramework.Serialization
{
    internal enum DataKind : byte
    {
        Null = 0xAA,
        Graph = 0xBB,
        Type = 0xCC,
        Assembly = 0xDD
    }
}
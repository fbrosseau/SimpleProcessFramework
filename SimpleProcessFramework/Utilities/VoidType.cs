namespace Spfx.Utilities
{
    internal struct VoidType
    {
        public static readonly VoidType Value;
        public static object BoxedValue { get; } = Value;
    }
}

namespace Spfx.Utilities
{
    internal struct VoidType
    {
        public static VoidType Value = new VoidType();
        public static object BoxedValue { get; } = Value;
    }
}

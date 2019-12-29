using System.Runtime.CompilerServices;

namespace Spfx.Utilities.ApiGlue
{
    internal static class UnsafeGlue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TOut As<TIn, TOut>(TIn val)
        {
#if NETCOREAPP3_0_PLUS || NETSTANDARD2_1_PLUS
            return Unsafe.As<TIn, TOut>(ref val);
#else
            return (TOut)(object)val;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T As<T>(object o)
            where T : class
        {
#if NETCOREAPP3_0_PLUS || NETSTANDARD2_1_PLUS
            return Unsafe.As<T>(o);
#else
            return (T)o;
#endif
        }
    }
}

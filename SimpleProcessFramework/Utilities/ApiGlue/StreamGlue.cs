using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    internal static class StreamGlue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static ValueTask DisposeAsync(this Stream s)
        {
            s.Dispose();
            return default;
        }
#else
        public static ValueTask DisposeAsync(Stream s)
        {
            return s.DisposeAsync();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static Task CopyToAsync(this Stream s, Stream other, CancellationToken ct)
        {
            return s.CopyToAsync(other);
        }
#else
        public static Task CopyToAsync(Stream s, Stream other, CancellationToken ct)
        {
            return s.CopyToAsync(other, ct);
        }
#endif
    }
}

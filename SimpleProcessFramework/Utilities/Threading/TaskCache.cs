using Spfx.Utilities.ApiGlue;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal static class TaskCache
    {
        internal static readonly Task<bool> TrueTask = Task.FromResult(true);
        internal static readonly Task<bool> FalseTask = GetDefaultValuedTask<bool>();
        internal static readonly Task<VoidType> VoidTypeTask = GetDefaultValuedTask<VoidType>();

        public static Task<T> FromResult<T>(T val)
        {
            if (typeof(T) == typeof(bool))
                return UnsafeGlue.As<Task<T>>(UnsafeGlue.As<T, bool>(val) ? TrueTask : FalseTask);

            if (val is null)
                return GetDefaultValuedTask<T>();

            return Task.FromResult(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task<T> GetDefaultValuedTask<T>()
        {
            return TaskHelper<T>.DefaultSuccessTask;
        }

        private static class TaskHelper<T>
        {
            public static readonly Task<T> DefaultSuccessTask = Task.FromResult(default(T));
        }
    }
}
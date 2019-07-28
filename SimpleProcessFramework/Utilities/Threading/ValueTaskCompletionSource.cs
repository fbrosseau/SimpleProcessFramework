using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
#if !TODO
    internal class ValueTaskCompletionSource<TResult> : TaskCompletionSource<TResult>
    {
        public ValueTask<TResult> ValueTask => new ValueTask<TResult>(Task);

        public ValueTaskCompletionSource()
        {
        }

        public ValueTaskCompletionSource(bool completeAsynchronously)
            : base(completeAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None)
        {
        }
    }
#else
    internal class ValueTaskCompletionSource<TResult> : IValueTaskSource<TResult>
    {
        private static int s_nextToken;

        private ManualResetValueTaskSourceCore<TResult> m_source;
        public ValueTask<TResult> ValueTask => new ValueTask<TResult>(this, (short)Interlocked.Increment(ref s_nextToken));

        public ValueTaskCompletionSource()
        {
        }

        public ValueTaskCompletionSource(bool completeAsynchronously)
        {
            m_source.RunContinuationsAsynchronously = completeAsynchronously;
        }

        public TResult GetResult(short token) => m_source.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => m_source.GetStatus(token);
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => m_source.OnCompleted(continuation, state, token, flags);
    }
#endif
}
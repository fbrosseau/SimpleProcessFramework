using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spfx.Utilities.Threading
{
#if !TODO
#if !NETSTANDARD2_1_PLUS
    internal class ValueTaskCompletionSource<TResult> : TaskCompletionSource<TResult>
    {
           public ValueTask<TResult> ValueTaskOfT => new ValueTask<TResult>(Task);
        public ValueTask ValueTask => new ValueTask(Task);

        public ValueTaskCompletionSource(bool completeAsynchronously = false)
            : base(completeAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None)
        {
        }
    }
#else
    internal class ValueTaskCompletionSource<TResult> : IValueTaskSource<TResult>, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<TResult> m_source;

        public ValueTask<TResult> ValueTaskOfT => new ValueTask<TResult>(this, m_source.Version);
        public ValueTask ValueTask => new ValueTask(this, m_source.Version);

        public ValueTaskCompletionSource(bool completeAsynchronously = false)
        {
            m_source.RunContinuationsAsynchronously = completeAsynchronously;
        }

        protected void ResetValueTaskSource()
        {
            m_source.Reset();
        }

        public void TrySetCanceled()
        {
            TrySetException(new TaskCanceledException());
        }

        public void TrySetException(Exception ex)
        {
            m_source.SetException(ex);
        }

        public void TrySetResult(TResult result)
        {
            m_source.SetResult(result);
        }

        public ValueTaskSourceStatus GetStatus(short token) => m_source.GetStatus(token);
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => m_source.OnCompleted(continuation, state, token, flags);
        void IValueTaskSource.GetResult(short token) => GetResult(token);
        public TResult GetResult(short token) => m_source.GetResult(token);
    }
#endif
#endif
}
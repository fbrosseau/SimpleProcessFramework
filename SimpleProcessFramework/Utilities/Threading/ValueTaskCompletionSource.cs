using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
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
}
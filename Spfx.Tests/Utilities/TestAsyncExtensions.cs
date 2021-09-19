using Spfx.Utilities.Threading;
using System.Threading.Tasks;

namespace Spfx.Tests
{
    public static class TestAsyncExtensions
    {
        /// <summary>
        /// 'With Timeout', the default Test Timeout
        /// </summary>
        public static Task WT(this Task t)
        {
            return t.WithTimeout(CommonTestClass.DefaultTestTimeoutTimespan);
        }

        /// <summary>
        /// 'With Timeout', the default Test Timeout
        /// </summary>
        public static Task<T> WT<T>(this Task<T> t)
        {
            return t.WithTimeout(CommonTestClass.DefaultTestTimeoutTimespan);
        }

        /// <summary>
        /// 'With Timeout', the default Test Timeout
        /// </summary>
        public static Task WT(this ValueTask t)
        {
            return t.AsTask().WithTimeout(CommonTestClass.DefaultTestTimeoutTimespan);
        }

        /// <summary>
        /// 'With Timeout', the default Test Timeout
        /// </summary>
        public static Task<T> WT<T>(this ValueTask<T> t)
        {
            return t.AsTask().WithTimeout(CommonTestClass.DefaultTestTimeoutTimespan);
        }
    }
}
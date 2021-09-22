using Spfx.Utilities.Threading;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Tests.Utilities
{
    internal static class TestProcessExtensions
    {
        public static Task WaitForExitAsync(this Process proc)
        {
            return WaitForExitAsync(proc, Timeout.InfiniteTimeSpan);
        }

        public static Task<bool> WaitForExitAsync(this Process proc, TimeSpan timeout)
        {
            return WaitForExitAsync(proc, timeout, default);
        }

        public static Task<bool> WaitForExitAsync(this Process proc, CancellationToken ct)
        {
            return WaitForExitAsync(proc, Timeout.InfiniteTimeSpan, ct);
        }

        public static Task<bool> WaitForExitAsync(this Process proc, TimeSpan timeout, CancellationToken ct)
        {
            if (proc.HasExited)
                return Task.FromResult(true);

            var tcs = new TaskCompletionSource<bool>();

            proc.EnableRaisingEvents = true;

            EventHandler exitHandler = null;
            CancellationTokenRegistration ctr = default;
            Timer timer = null;

            void CleanupAllRegistrations()
            {
                proc.Exited -= exitHandler;
                timer?.DisposeAsync().FireAndForget();
                ctr.DisposeAsync().FireAndForget();
            }

            exitHandler = (sender, e) =>
            {
                if (tcs.TrySetResult(true))
                    CleanupAllRegistrations();
            };

            if (timeout != Timeout.InfiniteTimeSpan)
            {
                timer = new Timer(s =>
                {
                    if (tcs.TrySetResult(false))
                        CleanupAllRegistrations();
                }, null, Timeout.Infinite, Timeout.Infinite);
            }

            if (ct.CanBeCanceled)
            {
                ctr = ct.Register(() =>
                {
                    if (tcs.TrySetResult(false))
                        CleanupAllRegistrations();
                });
            }

            proc.Exited += exitHandler;

            return tcs.Task;
        }
    }
}
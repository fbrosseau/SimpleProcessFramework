﻿using FluentAssertions;
using NUnit.Framework;
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Tests.Integration;
using Spfx.Utilities;
using Spfx.Utilities.Runtime;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Tests
{
    [Timeout(DefaultTestTimeout)]
    public abstract class CommonTestClass
    {
        public static ITypeResolver DefaultTestResolver { get; } = DefaultTypeResolverFactory.CreateRootTypeResolver<TestTypeResolverFactory>();

        public static bool IsInMsTest { get; set; } = true;
        public static readonly bool Test32Bit = HostFeaturesHelper.Is32BitSupported;

#if DEBUG
        public const int DefaultTestTimeout = 30000;
#else
        public const int DefaultTestTimeout = 30000;
#endif

        public static readonly TimeSpan DefaultTestTimeoutTimespan = TimeSpan.FromMilliseconds(DefaultTestTimeout);

        private static readonly Lazy<ILogger> s_logger = new Lazy<ILogger>(() => DefaultTypeResolverFactory.DefaultTypeResolver.CreateSingleton<ILoggerFactory>().GetLogger(typeof(CommonTestClass)));

        protected static void Log(string msg)
        {
            s_logger.Value.Info?.Trace(msg);
        }

        public enum ThrowAction
        {
            NoThrow,
            Throw
        }

        [OneTimeSetUp]
        public virtual ValueTask ClassSetUp()
        {
            return default;
        }

        protected static async Task WaitForAsync(Func<bool> func, TimeSpan operationsTimeout)
        {
            if (func())
                return;

            var sw = Stopwatch.StartNew();
            while (true)
            {
                if (func())
                    return;

                if (sw.Elapsed >= operationsTimeout)
                    throw new TimeoutException();

                await Task.Delay(50);
            }
        }

        internal static void MaybeAssertThrows<TEx>(ThrowAction expectThrow, Action callback, Action<TEx> exceptionCallback)
            where TEx : Exception
        {
            if (expectThrow == ThrowAction.NoThrow)
                callback();
            else
                AssertThrows(callback, exceptionCallback);
        }

        internal static void AssertThrows<TEx>(Action callback, Action<TEx> exceptionCallback = null)
            where TEx : Exception
        {
            AssertThrows(callback, ex =>
            {
                Assert.IsTrue(typeof(TEx).IsInstanceOfType(ex), $"Expected the exception to be of type {typeof(TEx).FullName}, not {ex.GetType().FullName}");
                exceptionCallback?.Invoke((TEx)ex);
            });
        }

        internal static void AssertThrows(Action callback, Action<Exception> exceptionCallback = null)
        {
            Exception caughtEx = null;
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }

            Assert.IsNotNull(caughtEx, "The callback did not throw");
            Log("Caught " + caughtEx.GetType().FullName);
            exceptionCallback?.Invoke(caughtEx);
        }

        internal static ValueTask AssertThrowsAsync<TException>(Func<Task> callback, Action<TException> exceptionCallback = null)
            where TException : Exception
        {
            return AssertThrowsAsync(callback, ex =>
            {
                ex.Should().BeOfType<TException>();
                exceptionCallback?.Invoke((TException)ex);
            });
        }

        internal static async ValueTask AssertThrowsAsync(Func<Task> callback, Action<Exception> exceptionCallback = null)
        {
            Exception caughtEx = null;
            try
            {
                await callback().WT();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }

            Assert.IsNotNull(caughtEx, "The callback did not throw");
            Log("Caught " + caughtEx.GetType().FullName);
            exceptionCallback?.Invoke(caughtEx);
        }

        private static Task WrapWithUnhandledExceptions(Task task)
        {
            var unhandledEx = ExceptionReportingEndpoint.GetUnhandledExceptionTask();
            if (unhandledEx is null)
                return task;

            return WrapWithUnhandledExceptions(task.ContinueWith(t => { t.GetAwaiter().GetResult(); return VoidType.Value; }));
        }

        private static Task<T> WrapWithUnhandledExceptions<T>(Task<T> task)
        {
            var unhandledEx = ExceptionReportingEndpoint.GetUnhandledExceptionTask();
            if (unhandledEx is null)
                return task;

            var combined = Task.WhenAny(task, unhandledEx);
            return combined.ContinueWith(_ =>
            {
                if (ReferenceEquals(combined.Result, unhandledEx))
                    return Task.FromException<T>(new Exception("Remote process had an unhandled exception", unhandledEx.ExtractException()));

                return task;
            }).Unwrap();
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static void Unwrap(Task task)
        {
            var wrapped = WrapWithUnhandledExceptions(task);

            if (!wrapped.WaitSilent(DefaultTestTimeout))
                throw new TimeoutException();

            wrapped.WaitOrRethrow();
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static T Unwrap<T>(Task<T> task)
        {
            Unwrap((Task)task);
            return task.Result;
        }

        public static void ExpectException(Task task)
        {
            ExpectException<Exception>(task);
        }

        public static void ExpectException<TException>(Task task, Action<TException> inspectException = null)
            where TException : Exception
        {
            ExpectException(task, typeof(TException), exceptionCallback: ex => inspectException?.Invoke((TException)ex));
        }

        public static void ExpectException(Task task, Type expectedExceptionType, string expectedText = null, string expectedStackFrame = null, Action<Exception> exceptionCallback = null)
        {
            task = WrapWithUnhandledExceptions(task);

            if (!task.WaitSilent(TimeSpan.FromSeconds(30)))
                throw new TimeoutException();

            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            var ex = task.ExtractException();
            if (!expectedExceptionType.IsInstanceOfType(ex))
                Assert.Fail("Expected an exception of type " + expectedExceptionType.FullName + ", got " + ex.GetType().FullName + ".\r\nException:" + ex);

            if (!string.IsNullOrWhiteSpace(expectedText))
            {
                Assert.IsTrue(ex.Message.Contains(expectedText));
            }

            if (!string.IsNullOrWhiteSpace(expectedStackFrame))
            {
                Assert.IsTrue(ex.StackTrace?.Contains(expectedStackFrame));
            }

            exceptionCallback?.Invoke(ex);
        }

        public static void AssertRangeEqual<T>(IEnumerable<T> expectedValues, IEnumerable<T> actualValues)
        {
            var expected = expectedValues.ToArray();
            var actual = actualValues.ToArray();
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }
        }

        public static void DeleteFileIfExists(string file)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (!Path.IsPathRooted(file))
                    fileInfo = PathHelper.GetFileRelativeToBin(file);

                if (fileInfo.Exists)
                {
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                        fileInfo.Attributes &= ~FileAttributes.ReadOnly;

                    fileInfo.Delete();
                }
            }
            catch
            {
                // oh well.
            }
        }
    }
}

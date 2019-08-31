using NUnit.Framework;
using Spfx.Tests.Integration;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Tests
{
    internal static class TestUtilities
    {
#if DEBUG
        public const int DefaultTestTimeout = 30000;
#else
        public const int DefaultTestTimeout = 30000;
#endif

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

        public static void Unwrap(Task task)
        {
            var wrapped = WrapWithUnhandledExceptions(task);

            if (!wrapped.Wrap().Wait(TimeSpan.FromSeconds(DefaultTestTimeout)))
                throw new TimeoutException();

            // to rethrow the original clean exception
            wrapped.GetAwaiter().GetResult();
        }

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

            if (!task.Wrap().Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException();

            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            var ex = task.ExtractException();
            if (!expectedExceptionType.IsInstanceOfType(ex))
                Assert.Fail("Expected an exception of type " + expectedExceptionType.FullName + ", got " + ex.GetType().FullName);

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
    }
}

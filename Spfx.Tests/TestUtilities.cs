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
        public const int DefaultTestTimeout = 300000;
#else
        public const int DefaultTestTimeout = 30000;
#endif

        public static void Unwrap(Task task)
        {
            var unhandledEx = ExceptionReportingEndpoint.GetUnhandledExceptionTask();
            var choices = new[] { task, unhandledEx };
            var winner = Task.WaitAny(choices, TimeSpan.FromSeconds(DefaultTestTimeout));

            if (winner == -1)
                throw new TimeoutException();

            // to rethrow the original clean exception
            if (winner == 0)
                task.GetAwaiter().GetResult();
            else
                throw new Exception("Remote process had an unhandled exception", unhandledEx.ExtractException());
        }

        public static T Unwrap<T>(Task<T> task)
        {
            Unwrap((Task)task);
            return task.Result;
        }

        public static void UnwrapException<TException>(Task task, Action<TException> inspectException = null)
            where TException : Exception
        {
            UnwrapException(task, typeof(TException), exceptionCallback: ex => inspectException((TException)ex));
        }

        public static void UnwrapException(Task task, Type expectedExceptionType, string expectedText = null, string expectedStackFrame = null, Action<Exception> exceptionCallback = null)
        {
            if (!task.Wrap().Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException();

            Assert.AreEqual(TaskStatus.Faulted, task.Status);
            var ex = task.ExtractException();
            if (!expectedExceptionType.IsAssignableFrom(ex.GetType()))
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

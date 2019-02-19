using NUnit.Framework;
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
        public static void Unwrap(Task task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException();
        }

        public static T Unwrap<T>(Task<T> task)
        {
            Unwrap((Task)task);
            return task.Result;
        }

        public static void UnwrapException(Task task, Type expectedExceptionType, string expectedText = null, string expectedStackFrame = null)
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

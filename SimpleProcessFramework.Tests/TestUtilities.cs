﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Tests
{
    internal static class TestUtilities
    {
        public static void Unwrap(Task task)
        {
            task.Wait();
        }

        public static T Unwrap<T>(Task<T> task)
        {
            return task.Result;
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
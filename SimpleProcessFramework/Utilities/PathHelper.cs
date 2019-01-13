using System;
using System.IO;

namespace SimpleProcessFramework.Utilities
{
    internal static class PathHelper
    {
        public static readonly DirectoryInfo BinFolder = new FileInfo(new Uri(typeof(PathHelper).Assembly.Location, UriKind.Absolute).LocalPath).Directory;

        public static FileInfo GetFileRelativeToBin(string filePath)
        {
            return new FileInfo(Path.Combine(BinFolder.FullName, filePath));
        }
    }
}

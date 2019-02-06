using System;
using System.IO;

namespace Spfx.Utilities
{
    internal static class PathHelper
    {
        public static readonly DirectoryInfo BinFolder = new FileInfo(new Uri(typeof(PathHelper).Assembly.Location, UriKind.Absolute).LocalPath).Directory;

        public static FileInfo GetFileRelativeToBin(string filePath)
        {
            return new FileInfo(Path.Combine(BinFolder.FullName, filePath));
        }

        public static string RealSystem32Folder { get; } =
            Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem
                ? Environment.SystemDirectory 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative");
    }
}

using System;
using System.IO;

namespace Spfx.Utilities
{
    internal static class PathHelper
    {
        public static readonly DirectoryInfo CurrentBinFolder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); // new FileInfo(new Uri(typeof(PathHelper).Assembly.Location, UriKind.Absolute).LocalPath).Directory;

        public static string GetPathRelativeToBin(string filePath)
        {
            return Path.GetFullPath(Path.Combine(CurrentBinFolder.FullName, filePath));
        }

        public static FileInfo GetFileRelativeToBin(string filePath)
        {
            return new FileInfo(GetPathRelativeToBin(filePath));
        }

        public static string RealSystem32Folder { get; } =
            Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem
                ? Environment.SystemDirectory 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative");
    }
}

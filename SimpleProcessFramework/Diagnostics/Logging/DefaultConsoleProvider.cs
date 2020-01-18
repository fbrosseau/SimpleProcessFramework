using System;
using System.IO;

namespace Spfx.Diagnostics.Logging
{
    public class DefaultConsoleProvider : IConsoleProvider
    {
        public TextWriter Out => Console.Out;
        public TextWriter Err => Console.Error;
    }
}

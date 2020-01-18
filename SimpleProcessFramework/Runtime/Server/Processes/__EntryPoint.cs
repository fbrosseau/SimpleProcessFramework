using Spfx.Runtime.Server.Processes.Hosting;
using Spfx.Subprocess;
using Spfx.Utilities.Runtime;
using System;
using System.IO;
using System.Linq;

namespace Spfx.Runtime.Server.Processes
{
    // Found by reflection!
    public class __EntryPoint
    {
        public static void Run()
        {
            if (Environment.GetCommandLineArgs().Contains(SubprocessMainShared.DescribeCmdLineArg))
            {
                Console.Write(HostFeaturesHelper.DescribeHost());
                Environment.Exit(SubprocessMainShared.DescribeExitCode);
            }

            Run(Console.In, isStandaloneProcess: true);
        }

        public static void Run(TextReader input, bool isStandaloneProcess)
        {
            bool graceful = false;
            ProcessContainer container = null;
            try
            {
                container = new ProcessContainer();
                container.Initialize(input);
                if (isStandaloneProcess)
                    container.Run();
                graceful = true;
            }
            catch(Exception ex)
            {
                TraceFatalException(ex);
            }
            finally
            {
                if (isStandaloneProcess)
                {
                    try
                    {
                        container?.Dispose();
                    }
                    finally
                    {
                        Environment.Exit(graceful ? 0 : -1);
                    }
                }
            }
        }

        private static void TraceFatalException(Exception ex)
        {
            Console.Error.WriteLine($"FATAL EXCEPTION -------{Environment.NewLine}{ex}");
        }
    }
}
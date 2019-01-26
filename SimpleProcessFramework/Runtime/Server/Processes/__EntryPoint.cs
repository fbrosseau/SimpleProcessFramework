using System;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    // Found by reflection!
    public class __EntryPoint
    {
        public static void Run()
        {
            bool graceful = false;
            try
            {
                using (var container = new ProcessContainer())
                {
                    container.Initialize();
                    container.Run();
                    graceful = true;
                }

            }
            finally
            {
                Environment.Exit(graceful ? 0 : -1);
            }
        }
    }
}

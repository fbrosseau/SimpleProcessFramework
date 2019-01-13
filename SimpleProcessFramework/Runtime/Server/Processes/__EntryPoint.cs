using System;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    // Found by reflection!
    public class __EntryPoint
    {
        public static void Run(string[] args)
        {
            using (var container = new ProcessContainer())
            {
                container.Initialize();
                container.Run();
            }
        }
    }
}

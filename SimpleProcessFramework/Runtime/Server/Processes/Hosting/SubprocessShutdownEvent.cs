using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class SubprocessShutdownEvent
    {
        public Task WaitTask { get; }
        public string Description { get; }

        public SubprocessShutdownEvent(Task task, string description)
        {
            WaitTask = task;
            Description = description;
        }
    }
}
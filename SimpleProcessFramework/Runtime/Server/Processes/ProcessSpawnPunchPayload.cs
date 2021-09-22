using Spfx.Interfaces;
using Spfx.Subprocess;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading;

namespace Spfx.Runtime.Server.Processes
{
    public class ProcessSpawnPunchPayload
    {
        public string HostAuthority { get; set; }
        public string ProcessUniqueId { get; set; }
        public ProcessKind ProcessKind { get; set; }
        public string IntegrityLevel { get; set; }
        public string WritePipe { get; set; }
        public string ReadPipe { get; set; }
        public string ShutdownEvent { get; set; }
        public int ParentProcessId { get; set; }
        public int HandshakeTimeout { get; set; }
        public string TypeResolverFactory { get; set; }

        public string SerializeToString()
        {
            var sw = new StringWriter();
            sw.WriteLine(HostAuthority);
            sw.WriteLine(ProcessUniqueId);
            sw.WriteLine(ProcessKind);
            sw.WriteLine(IntegrityLevel);
            sw.WriteLine(WritePipe);
            sw.WriteLine(ReadPipe);
            sw.WriteLine(ShutdownEvent);
            sw.WriteLine(ParentProcessId);
            sw.WriteLine(HandshakeTimeout);
            sw.WriteLine(TypeResolverFactory);
            return sw.ToString();
        }

        public static ProcessSpawnPunchPayload Deserialize(string textForm)
        {
            return Deserialize(new StringReader(textForm));
        }

        public static ProcessSpawnPunchPayload Deserialize(TextReader reader)
        {
            int timedOut = -1;

            var timeoutTimer = new Timer(_ =>
            {
                if (Interlocked.Exchange(ref timedOut, 1) != -1)
                    return;

                Console.Error.WriteLine("Timeout waiting for parent process input");
                SubprocessMainShared.FinalExitProcess(SubprocessExitCodes.InitFailure);
            }, null, TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);

            var output = new ProcessSpawnPunchPayload
            {
                HostAuthority = reader.ReadLine(),
                ProcessUniqueId = reader.ReadLine(),
                ProcessKind = (ProcessKind)Enum.Parse(typeof(ProcessKind), reader.ReadLine()),
                IntegrityLevel = reader.ReadLine(),
                WritePipe = reader.ReadLine(),
                ReadPipe = reader.ReadLine(),
                ShutdownEvent = reader.ReadLine(),
                ParentProcessId = int.Parse(reader.ReadLine()),
                HandshakeTimeout = int.Parse(reader.ReadLine()),
                TypeResolverFactory = reader.ReadLine()
            };

            timeoutTimer.DisposeAsync().FireAndForget();
            Interlocked.Exchange(ref timedOut, 0);

            return output;
        }
    }
}
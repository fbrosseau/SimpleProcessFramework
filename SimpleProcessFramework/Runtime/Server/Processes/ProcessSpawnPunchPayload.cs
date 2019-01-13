using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using SimpleProcessFramework.Utilities;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    public class ProcessSpawnPunchPayload
    {
        public string HostAuthority { get; set; }
        public string ProcessUniqueId { get; set; }
        public string ProcessKind { get; set; }
        public string IntegrityLevel { get; set; }
        public string WritePipe { get; set; }
        public string ReadPipe { get; set; }
        public string ShutdownEvent { get; set; }
        public int ParentProcessId { get; set; }

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
            return sw.ToString();
        }

        public static ProcessSpawnPunchPayload Deserialize(string textForm)
        {
            return Deserialize(new StringReader(textForm));
        }

        public static ProcessSpawnPunchPayload Deserialize(TextReader reader)
        {
            var readTask = Task.Run(() =>
            {
                var output = new ProcessSpawnPunchPayload
                {
                    HostAuthority = reader.ReadLine(),
                    ProcessUniqueId = reader.ReadLine(),
                    ProcessKind = reader.ReadLine(),
                    IntegrityLevel = reader.ReadLine(),
                    WritePipe = reader.ReadLine(),
                    ReadPipe = reader.ReadLine(),
                    ShutdownEvent = reader.ReadLine(),
                    ParentProcessId = int.Parse(reader.ReadLine())
                };
                return output;
            });

            return readTask.WaitOrTimeout(TimeSpan.FromSeconds(5));
        }
    }
}
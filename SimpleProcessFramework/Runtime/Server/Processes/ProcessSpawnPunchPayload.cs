using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Spfx.Utilities.Threading;
using Spfx.Interfaces;

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
                    ProcessKind = (ProcessKind)Enum.Parse(typeof(ProcessKind), reader.ReadLine()),
                    IntegrityLevel = reader.ReadLine(),
                    WritePipe = reader.ReadLine(),
                    ReadPipe = reader.ReadLine(),
                    ShutdownEvent = reader.ReadLine(),
                    ParentProcessId = int.Parse(reader.ReadLine())
                };
                return output;
            });

            try
            {
                return readTask.WaitOrTimeout(TimeSpan.FromSeconds(5));
            }
            catch
            {
                Console.Error.WriteLine("Timeout waiting for parent process input");
                throw;
            }
        }

        public static string SerializeHandle(SafeHandle safeHandle)
        {
            return SerializeHandle(safeHandle.DangerousGetHandle());
        }

        public static string SerializeHandle(IntPtr intPtr)
        {
            return intPtr.ToInt64().ToString();
        }

        public static SafeHandle DeserializeHandleFromString(string str)
        {
            return new Win32SafeHandle(new IntPtr(long.Parse(str)));
        }
    }

    internal class Win32SafeHandle : SafeHandle
    {
        public override bool IsInvalid => handle == (IntPtr)(-1);

        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public Win32SafeHandle(IntPtr val)
            : base((IntPtr)(-1), true)
        {
            handle = val;
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }
}
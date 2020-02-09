using Spfx.Interfaces;
using Spfx.Utilities.Runtime;
using Spfx.Utilities.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal class NamelessNamedPipePair
    {
        private static readonly int s_currentProcessId = Process.GetCurrentProcess().Id;

        public PipeStream LocalPipe { get; }
        public SafeHandle RemoteProcessPipe { get; }

        public NamelessNamedPipePair(PipeStream serverConnect, SafeHandle clientConnect)
        {
            LocalPipe = serverConnect;
            RemoteProcessPipe = clientConnect;
        }

        public static async Task<NamelessNamedPipePair> CreatePair(FileAccess localAccess = FileAccess.ReadWrite, FileAccess remoteAccess = FileAccess.ReadWrite, bool remoteIsAsync = true)
        {
            var pipename = $"Spfx_IpcPrivatePipe_{s_currentProcessId}_{Guid.NewGuid():N}";
            var fullname = @"\\.\pipe\" + pipename;
            var serverStream = CreateAsyncServerStream(pipename, localAccess);
            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            SafeHandle remotePipe = null;

            async Task CreateClientHandle()
            {
                while (true)
                {
                    try
                    {
                        remotePipe = Win32Interop.SafeCreateFile(fullname, remoteAccess, FileShare.None, FileMode.Open, async: remoteIsAsync);
                        if (remotePipe?.IsInvalid == false)
                            return;
                        remotePipe?.Dispose();
                    }
                    catch
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                    }
                }
            }

            try
            {
                var serverConnect = serverStream.WaitForConnectionAsync(ct);
                var clientConnect = CreateClientHandle();

                var combinedConnect = TaskEx.WhenAllOrRethrow(serverConnect, clientConnect);

                if (!await combinedConnect.WaitAsync(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Loopback connection should have been instantaneous!");

                return new NamelessNamedPipePair(serverStream, remotePipe);
            }
            catch
            {
                cts.SafeCancel();
                throw;
            }
        }

        private static NamedPipeServerStream CreateAsyncServerStream(string pipeName, FileAccess access)
        {
            PipeDirection dir = 0;
            if ((access & FileAccess.Read) == FileAccess.Read)
                dir |= PipeDirection.In;
            if ((access & FileAccess.Write) == FileAccess.Write)
                dir |= PipeDirection.Out;

            if (HostFeaturesHelper.LocalProcessIsNetfx)
            {
                const string pipesAssembly = ", System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
                var pipeSecurityType = Type.GetType("System.IO.Pipes.PipeSecurity" + pipesAssembly);
                var pipeSecurity = Activator.CreateInstance(pipeSecurityType);

                var pipeAccessRuleType = Type.GetType("System.IO.Pipes.PipeAccessRule" + pipesAssembly);
                var pipeAccessRightsType = Type.GetType("System.IO.Pipes.PipeAccessRights" + pipesAssembly);
                var accessControlTypeType = Type.GetType("System.Security.AccessControl.AccessControlType");

                var windowsIdentityType = Type.GetType("System.Security.Principal.WindowsIdentity");
                using (var currentIdentity = (IDisposable)windowsIdentityType.GetMethod("GetCurrent", Type.EmptyTypes).Invoke(null, null))
                {
                    var sid = windowsIdentityType.GetProperty("Owner").GetValue(currentIdentity);

                    var rule = Activator.CreateInstance(pipeAccessRuleType, sid, Enum.Parse(pipeAccessRightsType, "FullControl"), Enum.Parse(accessControlTypeType, "Allow"));

                    pipeSecurityType.GetMethod("AddAccessRule", new[] { pipeAccessRuleType }).Invoke(pipeSecurity, new[] { rule });
                    pipeSecurityType.GetMethod("SetOwner", new[] { sid.GetType() }).Invoke(pipeSecurity, new[] { sid });
                }

                var ctor = typeof(NamedPipeServerStream).GetConstructor(new[] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), pipeSecurityType, typeof(HandleInheritability) });
                return (NamedPipeServerStream)ctor.Invoke(new[]
                {
                    pipeName, dir, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity, HandleInheritability.None
                });
            }
            else
            {
                const PipeOptions CurrentUserOnly = (PipeOptions)0x20000000;
                return new NamedPipeServerStream(pipeName, dir, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | CurrentUserOnly);
            }
        }
    }
}
using Spfx.Diagnostics.Logging;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal class WindowsConsoleRedirector : Disposable
    {
        private readonly NamelessNamedPipePair m_outPair;
        private readonly NamelessNamedPipePair m_errPair;
        private readonly IConsoleConsumer m_consumer;

        public SafeHandle RemoteProcessOut => m_outPair.RemoteProcessPipe;
        public SafeHandle RemoteProcessErr => m_errPair.RemoteProcessPipe;

        private WindowsConsoleRedirector(IConsoleConsumer consumer, NamelessNamedPipePair outPair, NamelessNamedPipePair errPair)
        {
            m_outPair = outPair;
            m_errPair = errPair;
            m_consumer = consumer;
        }

        public static async Task<WindowsConsoleRedirector> CreateAsync(IConsoleConsumer consumer)
        {
            var outPair = await NamelessNamedPipePair.CreatePair();
            var errPair = await NamelessNamedPipePair.CreatePair();

            return new WindowsConsoleRedirector(consumer, outPair, errPair);
        }

        public void StartReading()
        {
            RedirectConsole(m_consumer, m_outPair.LocalPipe, StandardConsoleStream.Out).FireAndForget();
            RedirectConsole(m_consumer, m_errPair.LocalPipe, StandardConsoleStream.Error).FireAndForget();

            m_outPair.RemoteProcessPipe.SetHandleAsInvalid();
            m_outPair.RemoteProcessPipe.Dispose();
            m_errPair.RemoteProcessPipe.SetHandleAsInvalid();
            m_errPair.RemoteProcessPipe.Dispose();
        }

        private static async Task RedirectConsole(IConsoleConsumer consumer, Stream stream, StandardConsoleStream streamKind)
        {
            Exception caughtEx = null;
            try
            {
                using var reader = new StreamReader(stream, Console.OutputEncoding, true);
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null)
                        break;

                    consumer.ReportConsoleOutput(streamKind, line);
                }
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }
            finally
            {
                consumer.ReportStreamClosed(streamKind, caughtEx);
            }
        }

        protected override void OnDispose()
        {
            m_outPair.RemoteProcessPipe.Dispose();
            m_errPair.RemoteProcessPipe.Dispose();
            base.OnDispose();
        }
    }
}

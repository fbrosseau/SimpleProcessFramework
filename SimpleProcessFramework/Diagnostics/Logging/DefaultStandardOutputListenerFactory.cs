using Spfx.Reflection;
using System.Diagnostics;
using System.IO;

namespace Spfx.Diagnostics.Logging
{
    public class DefaultStandardOutputListenerFactory : IStandardOutputListenerFactory
    {
        private readonly IConsoleProvider m_consoleProvider;
        private readonly bool m_stderrToNormal;

        public DefaultStandardOutputListenerFactory(ITypeResolver typeResolver)
        {
            m_consoleProvider = typeResolver.GetSingleton<IConsoleProvider>();
            m_stderrToNormal = typeResolver.GetSingleton<ProcessClusterConfiguration>().PrintErrorInRegularOutput;
        }

        protected virtual bool RedirectErrorToStdOut => m_stderrToNormal;

        protected virtual TextWriter OutputWriter => m_consoleProvider.Out;
        protected virtual TextWriter ErrorWriter => RedirectErrorToStdOut ? OutputWriter : m_consoleProvider.Err;

        public virtual IStandardOutputListener Create(Process proc, bool isOut)
        {
            TextWriter w;
            if (isOut)
                w = OutputWriter;
            else
                w = ErrorWriter;

            var fmt = proc.Id + ">{0}";
            return CreateListener(w, fmt);
        }

        protected virtual IStandardOutputListener CreateListener(TextWriter w, string fmt)
        {
            return new DefaultStandardOutputListener(w, fmt);
        }
    }

    public class DefaultStandardOutputListener : IStandardOutputListener
    {
        private readonly TextWriter m_writer;
        private readonly string m_format;

        public DefaultStandardOutputListener(TextWriter output, string outputFormat)
        {
            m_writer = output;
            m_format = outputFormat;
        }

        public void OutputReceived(string data)
        {
            m_writer.WriteLine(m_format, data);
        }

        public void Dispose()
        {
        }
    }
}

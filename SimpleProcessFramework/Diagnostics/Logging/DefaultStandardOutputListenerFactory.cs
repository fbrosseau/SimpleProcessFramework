using Spfx.Reflection;
using System;
using System.Diagnostics;
using System.IO;

namespace Spfx.Diagnostics.Logging
{
    public class DefaultStandardOutputListenerFactory : IStandardOutputListenerFactory
    {
        private readonly bool m_stderrToNormal;

        public DefaultStandardOutputListenerFactory(ITypeResolver typeResolver)
        {
            m_stderrToNormal = typeResolver.GetSingleton<ProcessClusterConfiguration>().PrintErrorInRegularOutput;
        }

        protected virtual bool RedirectErrorToStdOut => m_stderrToNormal;

        public virtual IStandardOutputListener Create(Process proc, bool isOut)
        {
            TextWriter w;
            if (isOut || RedirectErrorToStdOut)
                w = Console.Out;
            else
                w = Console.Error;

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

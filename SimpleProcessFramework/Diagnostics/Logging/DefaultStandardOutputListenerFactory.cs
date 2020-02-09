using Spfx.Reflection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Spfx.Diagnostics.Logging
{
    public class DefaultStandardOutputListenerFactory : IStandardOutputListenerFactory
    {
        private readonly IConsoleProvider m_consoleProvider;
        private readonly bool m_stderrToNormal;
        private readonly string m_rawFormat;

        public DefaultStandardOutputListenerFactory(ITypeResolver typeResolver)
        {
            m_consoleProvider = typeResolver.GetSingleton<IConsoleProvider>();

            var config = typeResolver.GetSingleton<ProcessClusterConfiguration>();

            m_stderrToNormal = config.PrintErrorInRegularOutput;
            m_rawFormat = config.ConsoleRedirectionOutputFormat;
        }

        protected virtual bool RedirectErrorToStdOut => m_stderrToNormal;

        protected virtual TextWriter OutputWriter => m_consoleProvider.Out;
        protected virtual TextWriter ErrorWriter => RedirectErrorToStdOut ? OutputWriter : m_consoleProvider.Err;

        protected delegate FormatCallback ValueProviderCallback(Process targetProcess, object friendlyProcessId, string formatArgument);
        protected delegate FormatCallback SimpleValueProviderCallback(string formatArgument);
        protected delegate string FormatCallback(string msg);

        private class PatternValueProvider
        {
            public string Pattern { get; }
            public ValueProviderCallback ValueProvider { get; }

            public PatternValueProvider(string pattern, SimpleValueProviderCallback valueProvider)
                : this(pattern, (p, id, fmt) => valueProvider(fmt))
            {
            }
            
            public PatternValueProvider(string pattern, ValueProviderCallback valueProvider)
            {
                Pattern = pattern;
                ValueProvider = valueProvider;
            }
        }

        private static readonly PatternValueProvider[] ValueProviders = new[]
        {
            new PatternValueProvider("%MSG%", fmtArg=>msg=>msg),
            new PatternValueProvider("%TIME%", fmtArg=>msg=>DateTime.Now.ToString(fmtArg)),
            new PatternValueProvider("%UTCTIME%", fmtArg=>msg=>DateTime.UtcNow.ToString(fmtArg)),
            new PatternValueProvider("%PID%", (proc,procId,fmtArg)=>
            {
                if(procId is null)
                    procId = proc.Id.ToString(fmtArg, CultureInfo.CurrentCulture);
                var pid = procId.ToString();
                return msg=>pid;
            })
        };

        public virtual IStandardOutputListener Create(Process proc, StandardConsoleStream stream, object friendlyProcessId = null)
        {
            TextWriter w;
            if (stream == StandardConsoleStream.Out)
                w = OutputWriter;
            else
                w = ErrorWriter;

            var finalFormat = m_rawFormat;
            var valueCallbacks = new List<FormatCallback>();

            int nextIndex = 0;
            foreach (var provider in ValueProviders)
            {
                finalFormat = Regex.Replace(finalFormat, $"{{{provider.Pattern}(:(?<arg>.*?))?}}", m =>
                {
                    var arg = m.Groups["arg"].Value;
                    if (string.IsNullOrWhiteSpace(arg))
                        arg = null;

                    valueCallbacks.Add(provider.ValueProvider(proc, friendlyProcessId, arg));

                    return "{" + nextIndex++ + "}";
                });
            }

            if (valueCallbacks.Count == 0)
                throw new InvalidOperationException("Expected at least 1 replacement %MSG% !");

            var vals = valueCallbacks.ToArray();

            switch (valueCallbacks.Count)
            {
                case 1:
                    return CreateListener(w, (w, msg) =>
                    {
                        w.WriteLine(finalFormat, vals[0](msg));
                    });
                case 2:
                    return CreateListener(w, (w, msg) =>
                    {
                        w.WriteLine(finalFormat, vals[0](msg), vals[1](msg));
                    });
                default:
                    return CreateListener(w, (w, msg) =>
                    {
                        var args = new object[vals.Length];
                        for (int i = 0; i < vals.Length; ++i)
                        {
                            args[i] = vals[i](msg);
                        }

                        w.WriteLine(finalFormat, args);
                    });
            }
        }

        protected virtual IStandardOutputListener CreateListener(TextWriter w, Action<TextWriter, string> fmt)
        {
            return new DefaultStandardOutputListener(w, fmt);
        }
    }

    public class DefaultStandardOutputListener : IStandardOutputListener
    {
        private readonly TextWriter m_writer;
        private readonly Action<TextWriter, string> m_format;

        public DefaultStandardOutputListener(TextWriter output, Action<TextWriter, string> outputFormat)
        {
            m_writer = output;
            m_format = outputFormat;
        }

        public void OutputReceived(string data)
        {
            m_format(m_writer, data);
        }

        public void Dispose()
        {
        }
    }
}

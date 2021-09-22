#if WINDOWS_BUILD

using Spfx.Interfaces;
using Spfx.Utilities.ApiGlue;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal partial class WindowsProcessTargetHandle
    {
        private class WindowsEnvironmentVariablesBlockBuilder
        {
            private static readonly Dictionary<string, string> s_defaultVariables =
                Environment.GetEnvironmentVariables()
                    .Cast<object>()
                    .Select(o => (DictionaryEntry)o)
                    .Where(kvp => kvp.Key != null)
                    .ToDictionary(kvp => (string)kvp.Key, kvp => kvp.Value?.ToString() ?? "");

            private static readonly UnicodeEncoding s_unicodeNoBom = new (false, true);

            private MemoryStream m_blockWriterStream;
            private StreamWriter m_blockWriter;
            private Dictionary<string, string> m_environmentVariables;
            private List<KeyValuePair<string, string>> m_sortedEnvironmentVariablesList;
            private byte[] m_finalBlock;

            public WindowsEnvironmentVariablesBlockBuilder()
            {
                m_sortedEnvironmentVariablesList = new List<KeyValuePair<string, string>>();
                m_environmentVariables = new Dictionary<string, string>(s_defaultVariables.Count + 32);

                foreach(var kvp in s_defaultVariables)
                {
                    AddVariable(kvp.Key, kvp.Value);
                }

                m_blockWriterStream = new MemoryStream();
                m_blockWriter = new StreamWriter(m_blockWriterStream, s_unicodeNoBom);
            }

            public void AddVariable(string key, string value)
            {
                m_environmentVariables[key] = value;
            }

            internal void AddVariables(Dictionary<string, string> vars)
            {
                if (vars is null)
                    return;

                foreach(var kvp in vars)
                {
                    AddVariable(kvp.Key, kvp.Value);
                }
            }

            internal void AddVariables(IEnumerable<StringKeyValuePair> vars)
            {
                if (vars is null)
                    return;

                foreach (var kvp in vars)
                {
                    AddVariable(kvp.Key, kvp.Value);
                }
            }

            public byte[] CreateFinalEnvironmentBlock()
            {
                if (m_finalBlock != null)
                    return m_finalBlock;

                EnsureCapacity(0);

                foreach (var kvp in m_environmentVariables)
                {
                    m_sortedEnvironmentVariablesList.Add(kvp);
                }

                m_environmentVariables = null;

                m_sortedEnvironmentVariablesList.Sort((kvp1, kvp2) => kvp1.Key.CompareTo(kvp2.Key));

                foreach (var kvp in m_sortedEnvironmentVariablesList)
                {
                    m_blockWriter.Write(kvp.Key);
                    m_blockWriter.Write('=');
                    m_blockWriter.Write(kvp.Value);
                    m_blockWriter.Write('\0');
                }

                m_sortedEnvironmentVariablesList = null;

                m_blockWriter.Write('\0');
                m_blockWriter.Flush();
                m_finalBlock = m_blockWriterStream.GetBuffer();
                m_blockWriterStream = null;
                m_blockWriter = null;

                return m_finalBlock;
            }

            internal void EnsureExtraCapacity()
            {
                EnsureCapacity(+32);
            }

            private void EnsureCapacity(int extraExpectedVariables = 0)
            {
                var estimatedTotalCount = m_environmentVariables.Count + extraExpectedVariables;
                m_environmentVariables.EnsureCapacity(estimatedTotalCount);

                if (m_sortedEnvironmentVariablesList.Capacity < estimatedTotalCount)
                    m_sortedEnvironmentVariablesList.Capacity = estimatedTotalCount;

                var estimatedBytes = UnicodeEncoding.CharSize * (EstimateResultStringLength(m_environmentVariables) + extraExpectedVariables * 32) + 32;
                if (m_blockWriterStream.Capacity < estimatedBytes)
                    m_blockWriterStream.Capacity = estimatedBytes;
            }

            private static int EstimateResultStringLength(Dictionary<string, string> envVars)
            {
                int strLen = 0;
                foreach (var kvp in envVars)
                {
                    strLen += kvp.Key.Length + kvp.Value.Length + 2;
                }

                return strLen + 1;
            }
        }
    }
}

#endif
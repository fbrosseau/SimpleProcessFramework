using Spfx.Subprocess;
using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    public class SubprocessLostException : SerializableException
    {
        [DataMember]
        public int ExitCodeNumber { get; set; }

        [DataMember]
        internal SubprocessMainShared.SubprocessExitCodes ExitCode { get; set; }

        public SubprocessLostException(string msg, Exception innerEx = null)
            : base(msg, innerEx)
        {
        }
    }
}
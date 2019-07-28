using System;
using System.Runtime.Serialization;

namespace Spfx.Interfaces
{
    [DataContract]
    public class ProcessCreationInfo
    {
        [DataMember]
        public TargetFramework TargetFramework { get; set; } = TargetFramework.Default;

        [DataMember]
        public string ProcessName { get; set; }

        [DataMember]
        public KeyValuePair[] ExtraEnvironmentVariables { get; set; }

        [DataMember]
        public string[] ExtraCommandLineArguments { get; set; }

        [DataMember]
        public string ProcessUniqueId { get; set; }

        [DataMember]
        public bool ManuallyRedirectConsole { get; set; }

        public void EnsureIsValid()
        {
            if (string.IsNullOrWhiteSpace(ProcessUniqueId))
                throw new InvalidOperationException(nameof(ProcessUniqueId) + " is mandatory");
            if (TargetFramework is null)
                throw new InvalidOperationException(nameof(TargetFramework) + " is mandatory");
        }

        [DataContract]
        public class KeyValuePair
        {
            [DataMember]
            public string Key { get; set; }
            [DataMember]
            public string Value { get; set; }

            public KeyValuePair()
            {
            }

            public KeyValuePair(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}

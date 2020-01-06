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
        public StringKeyValuePair[] ExtraEnvironmentVariables { get; set; }

        [DataMember]
        public string[] ExtraCommandLineArguments { get; set; }

        [DataMember]
        public string ProcessUniqueId { get; set; }

        [DataMember]
        public bool ManuallyRedirectConsole { get; set; }

        [DataMember]
        public string RuntimeVersionOverride { get; set; }

        [DataMember]
        public bool? Append32BitSuffix { get; set; }

        public void EnsureIsValid()
        {
            if (string.IsNullOrWhiteSpace(ProcessUniqueId))
                throw new InvalidOperationException(nameof(ProcessUniqueId) + " is mandatory");
            if (TargetFramework is null)
                throw new InvalidOperationException(nameof(TargetFramework) + " is mandatory");
        }
    }

    [DataContract]
    public class StringKeyValuePair
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string Value { get; set; }

        public StringKeyValuePair()
        {
        }

        public StringKeyValuePair(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}

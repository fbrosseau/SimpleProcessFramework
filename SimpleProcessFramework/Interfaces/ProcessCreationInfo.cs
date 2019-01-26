using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Interfaces
{
    [DataContract]
    public class ProcessCreationInfo
    {
        [DataMember]
        public ProcessKind ProcessKind { get; set; } = ProcessKind.Default;

        [DataMember]
        public string ProcessName { get; set; }

        [DataMember]
        public List<KeyValuePair> ExtraEnvironmentVariables { get; set; }

        [DataMember]
        public string ExtraCommandLine { get; set; }

        [DataMember]
        public string ProcessUniqueId { get; set; }

        public void EnsureIsValid()
        {
            if (string.IsNullOrWhiteSpace(ProcessUniqueId))
                throw new InvalidOperationException(nameof(ProcessUniqueId) + " is mandatory");
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

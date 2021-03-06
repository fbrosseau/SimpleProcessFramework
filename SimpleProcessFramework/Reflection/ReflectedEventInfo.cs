﻿using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Reflection
{
    [DataContract]
    public class ReflectedEventInfo : IEquatable<ReflectedEventInfo>
    {
        private EventInfo m_resolvedEvent;

        [DataMember]
        public ReflectedTypeInfo Type { get; set; }

        [DataMember]
        public string Name { get; set; }

        public EventInfo ResolvedEvent
        {
            get
            {
                if (m_resolvedEvent is null)
                {
                    m_resolvedEvent = Type.ResolvedType.GetEvent(Name);
                    if (m_resolvedEvent is null)
                        throw new MissingMemberException(Type.ResolvedType.FullName, Name);
                }

                return m_resolvedEvent;
            }
        }

        public ReflectedEventInfo(EventInfo e, bool cacheVisitedTypes = false)
        {
            ReflectedTypeInfo GetType(Type t) => cacheVisitedTypes ? ReflectedTypeInfo.AddWellKnownType(t) : t;

            Name = e.Name;
            Type = GetType(e.DeclaringType);

            m_resolvedEvent = e;
        }

        public override bool Equals(object obj) { return Equals(obj as ReflectedEventInfo); }
        public override int GetHashCode() => Type.GetHashCode() ^ Name.GetHashCode();
        public override string ToString() => Name;

        public bool Equals(ReflectedEventInfo other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            return other.Name == Name && other.Type.Equals(Type);
        }
    }
}

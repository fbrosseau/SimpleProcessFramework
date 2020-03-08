using Spfx.Utilities;
using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Spfx.Reflection
{
    [DataContract(IsReference = true)]
    public sealed class ReflectedTypeInfo : IEquatable<ReflectedTypeInfo>
    {
        private static readonly ThreadSafeAppendOnlyDictionary<Type, ReflectedTypeInfo> s_knownTypes = new ThreadSafeAppendOnlyDictionary<Type, ReflectedTypeInfo>();

        private Type m_resolvedType;

        [DataMember]
        public ReflectedAssemblyInfo Assembly { get; private set; }

        [DataMember]
        public ReflectedTypeInfo[] GenericParameters { get; private set; }

        [DataMember]
        public string Name { get; private set; }

        static ReflectedTypeInfo()
        {
            foreach (var primitive in PrimitiveWellKnownTypes)
            {
                AddWellKnownType(primitive);
            }
        }

        public Type ResolvedType
        {
            get
            {
                if (m_resolvedType is null)
                {
                    m_resolvedType = Assembly.ResolvedAssembly.GetType(Name, throwOnError: true);
                    if (m_resolvedType is null)
                        throw new TypeLoadException("Could not load type " + Name + " from assembly " + Assembly.Name);
                }

                return m_resolvedType;
            }
        }

        private ReflectedTypeInfo(Type t)
        {
            Name = t.FullName;
            Assembly = ReflectedAssemblyInfo.Create(t.Assembly);
            m_resolvedType = t;

            if (t.IsGenericType)
                GenericParameters = t.GetGenericArguments().Select(a => new ReflectedTypeInfo(a)).ToArray();
        }

        public static implicit operator ReflectedTypeInfo(Type t)
        {
            return Create(t);
        }

        public static ReflectedTypeInfo Create(Type t)
        {
            if (s_knownTypes.TryGetValue(t, out var reflectedInfo))
                return reflectedInfo;

            return new ReflectedTypeInfo(t);
        }

        internal static ReflectedTypeInfo AddWellKnownType(Type t)
        {
            if (s_knownTypes.TryGetValue(t, out var ti))
                return ti;

            ti = new ReflectedTypeInfo(t);
            s_knownTypes.Add(t, ti);

            ReflectedAssemblyInfo.AddWellKnownAssembly(t.Assembly);

            return ti;
        }

        public override bool Equals(object obj) { return Equals(obj as ReflectedTypeInfo); }
        public override int GetHashCode() => Assembly.GetHashCode() ^ Name.GetHashCode();
        public override string ToString() => Name;

        public string GetShortName() => m_resolvedType?.Name ?? Name;

        public bool Equals(ReflectedTypeInfo other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            return other.Name == Name && other.Assembly.Equals(Assembly);
        }

        public static ReadOnlySpan<Type> PrimitiveWellKnownTypes => new[]
        {
            typeof(string),
            typeof(char),
            typeof(bool),
            typeof(sbyte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(byte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(Guid),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(object),
            typeof(IPAddress),
            typeof(IPEndPoint),
            typeof(DnsEndPoint),
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(Version),
            typeof(X509Certificate),
            typeof(CancellationToken)
        };
    }
}
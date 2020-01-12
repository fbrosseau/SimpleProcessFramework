using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Spfx.Serialization.Serializers;

namespace Spfx.Serialization.DataContracts
{
    internal class SingleMemberDataContractSerializer : BaseReflectedDataContractSerializer
    {
        private static readonly MethodInfo s_genericFactoryFactory = typeof(SingleMemberDataContractSerializer).GetMethod(nameof(CreateTypedFactoryProxy), BindingFlags.Static | BindingFlags.NonPublic);

        private readonly ReflectedDataMember m_singleMember;
        private readonly Func<object, object> m_factory;

        private SingleMemberDataContractSerializer(Type actualType, List<ReflectedDataMember> reflectedDataMembers, MethodInfo factory)
            : base(actualType, reflectedDataMembers)
        {
            m_singleMember = reflectedDataMembers.Single();
            if (m_singleMember.TypeInfo.MemberType == typeof(object))
            {
                m_factory = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), factory);
            }
            else
            {
                var funcType = typeof(Func<,>).MakeGenericType(m_singleMember.TypeInfo.MemberType, typeof(object));
                var realFactory = Delegate.CreateDelegate(funcType, factory);
                m_factory = (Func<object, object>)s_genericFactoryFactory.MakeGenericMethod(m_singleMember.TypeInfo.MemberType).Invoke(null, new object[] { realFactory });
            }
        }

        private static Func<object, object> CreateTypedFactoryProxy<TRealArg>(Func<TRealArg, object> realFactory)
        {
            return o => realFactory((TRealArg)o);
        }

        internal static bool TryCreate(Type actualType, List<ReflectedDataMember> members, out ITypeSerializer s)
        {
            var factory = actualType.GetMethod("CreateFromSerialization", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var args = factory?.GetParameters();
            if (args?.Length == 1 && args[0].ParameterType == members[0].TypeInfo.MemberType)
            {
                s = new SingleMemberDataContractSerializer(actualType, members, factory);
                return true;
            }

            s = null;
            return false;
        }

        private struct SingleMemberContractHandler : IDataContractDeserializationHandler
        {
            private object m_graph;
            private bool m_graphInitialized;
            private readonly SingleMemberDataContractSerializer m_serializer;

            public SingleMemberContractHandler(SingleMemberDataContractSerializer serializer) 
            {
                m_serializer = serializer;
                m_graph = null;
                m_graphInitialized = false;
            }

            public object GetFinalObject()
            {
                if (!m_graphInitialized)
                    throw new InvalidOperationException("The serialized object did not contain the required DataMember");

                return m_graph;
            }

            public void HandleMember(ReflectedDataMember expectedMember, object value)
            {
                if (!ReferenceEquals(expectedMember, m_serializer.m_singleMember))
                    return;

                m_graph = m_serializer.m_factory(value);
                m_graphInitialized = true;
            }

            public void HandleMissingMember(ReflectedDataMember expectedMember)
            {
            }
        }

        public override object ReadObject(DeserializerSession reader)
        {
            var h = new SingleMemberContractHandler(this);
            return ReadObject(ref h, reader);
        }
    }
}
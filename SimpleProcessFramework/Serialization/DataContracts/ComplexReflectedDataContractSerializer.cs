using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Serialization.DataContracts
{
    internal sealed class ComplexReflectedDataContractSerializer : BaseReflectedDataContractSerializer
    {
        private readonly Func<object> m_constructor;

        public ComplexReflectedDataContractSerializer(Type actualType, List<ReflectedDataMember> members)
            : base(actualType, members)
        {
            var defaultCtor = actualType.GetConstructor(Type.EmptyTypes);
            if (defaultCtor != null)
                m_constructor = () => Activator.CreateInstance(actualType);
            else
                m_constructor = () => FormatterServices.GetUninitializedObject(actualType);
        }

        private struct ComplexDataContractDeserializationHandler : IDataContractDeserializationHandler
        {
            private readonly object m_graph;

            public ComplexDataContractDeserializationHandler(object graph)
            {
                m_graph = graph;
            }

            public object GetFinalObject()
            {
                return m_graph;
            }

            public void HandleMember(ReflectedDataMember expectedMember, object value)
            {
                expectedMember.SetAccessor(m_graph, value);
            }

            public void HandleMissingMember(ReflectedDataMember expectedMember)
            {
                expectedMember.SetAccessor(m_graph, expectedMember.TypeInfo.DefaultValueForType);
            }
        }

        public override object ReadObject(DeserializerSession session)
        {
            var obj = m_constructor();

            if (IsSerializationAware)
                ((ISerializationAwareObject)obj).OnBeforeDeserialize(session);

            var handler = new ComplexDataContractDeserializationHandler(obj);
            return ReadObject(ref handler, session);
        }
    }
}
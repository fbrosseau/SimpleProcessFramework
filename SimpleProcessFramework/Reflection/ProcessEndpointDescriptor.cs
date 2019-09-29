﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace Spfx.Reflection
{
    [DataContract]
    public class ProcessEndpointDescriptor
    {
        [DataMember]
        public IReadOnlyCollection<ProcessEndpointMethodDescriptor> Methods { get; set; }

        [DataMember]
        public IReadOnlyCollection<string> Events { get; set; }

        public static ProcessEndpointDescriptor CreateFromCurrentProcess(Type type)
        {
            var allInterfaces = GetInterestingInterfaces(type).ToList();

            var methodDescriptors = new List<ProcessEndpointMethodDescriptor>();
            foreach (var m in allInterfaces.SelectMany(t => t.GetMethods()).Distinct().OrderBy(m => m.Name))
            {
                if (m.IsSpecialName)
                    continue;

                methodDescriptors.Add(new ProcessEndpointMethodDescriptor
                {
                    Method = new ReflectedMethodInfo(m),
                    MethodId = methodDescriptors.Count,
                    IsCancellable = m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken))
                });
            }

            var uniqueEventNames = new HashSet<string>();
            foreach (var e in allInterfaces.SelectMany(i => i.GetEvents()))
            {
                uniqueEventNames.Add(e.Name);
                ReflectedTypeInfo.AddWellKnownType(e.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType);
            }

            return new ProcessEndpointDescriptor
            {
                Methods = methodDescriptors.ToArray(),
                Events = uniqueEventNames.ToArray()
            };
        }

        private static IEnumerable<Type> GetInterestingInterfaces(Type t)
        {
            return new[] { t }.Union(t.GetInterfaces().Except(new[] { typeof(IDisposable) }));
        }
    }
}
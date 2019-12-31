﻿using Spfx.Utilities;
using Spfx.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal static class ProcessProxyFactory
    {
        private static class FactoryStorage<T>
        {
            internal static readonly Func<ProcessProxyImplementation> Func;

            static FactoryStorage()
            {
                Func = CreateFactory(ReflectionUtilities.GetType<T>());
            }
        }

        internal static ProcessProxyImplementation CreateImplementation<T>()
        {
            return FactoryStorage<T>.Func();
        }

        private static Func<ProcessProxyImplementation> CreateFactory(Type type)
        {
            Guard.ArgumentNotNull(type, nameof(type));

            if (!type.IsInterface)
                throw new ArgumentException("'T' must be an interface");

            DynamicCodeGenModule.IgnoreAccessChecks(type.Assembly);

            var typeBuilder = DynamicCodeGenModule.DynamicModule.DefineType("ProxyImpl__" + type.AssemblyQualifiedName,
                TypeAttributes.Public,
                typeof(ProcessProxyImplementation));

            typeBuilder.AddInterfaceImplementation(type);

            var factoryName = "Type Factory";
            var factoryMethodBuilder = typeBuilder.DefineMethod(factoryName,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                typeof(ProcessProxyImplementation),
                Type.EmptyTypes);

            var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var ctorIlgen = ctorBuilder.GetILGenerator();
            ctorIlgen.Emit(OpCodes.Ldarg_0);
            ctorIlgen.Emit(OpCodes.Call, ProcessProxyImplementation.Reflection.Ctor);

            var ilgen = factoryMethodBuilder.GetILGenerator();
            ilgen.Emit(OpCodes.Newobj, ctorBuilder);
            ilgen.Emit(OpCodes.Ret);

            var methodInfoFieldNames = new Dictionary<MethodInfo, string>();

            var allInterfaces = new[] { type }.Concat(type.GetInterfaces()).Distinct();
            var methods = allInterfaces.SelectMany(i => i.GetMethods()).Where(m => !m.IsSpecialName).ToList();
            foreach (var m in methods)
            {
                var parameters = m.GetParameters();
                var argTypes = parameters.Select(p => p.ParameterType).ToArray();
                var implBuilder = typeBuilder.DefineExactOverride(m);

                string baseName = "MethodInfo__" + m.Name;
                string actualName = baseName;
                int seed = 0;
                while(methodInfoFieldNames.Values.Contains(actualName))
                {
                    actualName = baseName + seed;
                    ++seed;
                }

                methodInfoFieldNames.Add(m, actualName);

                var methodInfoField = typeBuilder.DefineField(
                  actualName,
                    typeof(ReflectedMethodInfo),
                    FieldAttributes.Private | FieldAttributes.Static);

                ilgen = implBuilder.GetILGenerator();

                LocalBuilder loc = null;

                var cancellationTokenLocal = ilgen.DeclareLocal(typeof(CancellationToken));

                if (parameters.Length > 0)
                {
                    loc = ilgen.DeclareLocal(typeof(object[]));
                    ilgen.Emit(OpCodes.Ldc_I4, parameters.Length);
                    ilgen.Emit(OpCodes.Newarr, typeof(object));
                    ilgen.Emit(OpCodes.Stloc, loc);

                    for (int i = 0; i < parameters.Length; ++i)
                    {
                        ilgen.Emit(OpCodes.Ldloc, loc);
                        ilgen.Emit(OpCodes.Ldc_I4, i);
                        ilgen.Emit(OpCodes.Conv_I);

                        ilgen.Emit(OpCodes.Ldarg, i + 1);

                        var argType = argTypes[i];

                        if (argType == typeof(CancellationToken))
                        {
                            ilgen.Emit(OpCodes.Stloc, cancellationTokenLocal);
                            ilgen.Emit(OpCodes.Ldsfld, BoxHelper.Reflection.BoxedCancellationTokenField);
                        }
                        else
                        {
                            var primitiveType = argType;
                            if (argType.IsByRef)
                            {
                                primitiveType = argType.GetElementType();
                                ilgen.Emit(OpCodes.Ldobj, primitiveType);
                            }

                            if (primitiveType.IsValueType)
                            {
                                BoxHelper.Reflection.EmitBox(ilgen, primitiveType);
                            }
                        }

                        ilgen.Emit(OpCodes.Stelem_Ref);
                    }
                }

                ilgen.Emit(OpCodes.Ldarg_0);

                if (loc != null)
                    ilgen.Emit(OpCodes.Ldloc, loc);
                else
                    ilgen.Emit(OpCodes.Ldnull);

                ilgen.Emit(OpCodes.Ldsfld, methodInfoField);

                var retType = m.ReturnType;
                if (typeof(Task).IsAssignableFrom(retType))
                {
                    ilgen.Emit(OpCodes.Ldloc, cancellationTokenLocal);

                    if (retType == typeof(Task))
                    {
                        ilgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.WrapTaskReturnMethod, null);
                    }
                    else
                    {
                        ilgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.GetWrapTaskOfTReturnMethod(retType.GetGenericArguments()[0]), null);
                    }
                }
                else if (retType == typeof(ValueTask))
                {
                    ilgen.Emit(OpCodes.Ldloc, cancellationTokenLocal);
                    ilgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.WrapValueTaskReturnMethod, null);
                }
                else if(retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    ilgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.GetWrapValueTaskOfTReturnMethod(retType.GetGenericArguments()[0]), null);
                }

                ilgen.Emit(OpCodes.Ret);
            }

            string GetEventInfoField(EventInfo evt)
            {
                return "FieldInfo__" + evt.Name;
            }

            var events = allInterfaces.SelectMany(i => i.GetEvents()).ToList();
            if (events.Count > 0)
            {
                var eventBackingFields = new Dictionary<EventInfo, FieldBuilder>();
                foreach (var evt in events)
                {
                    var eventInfoField = typeBuilder.DefineField(GetEventInfoField(evt), typeof(ReflectedEventInfo), FieldAttributes.Private | FieldAttributes.Static);
                    var backingField = typeBuilder.DefineField("Event__" + evt.Name, typeof(ProcessProxyEventSubscriptionInfo), FieldAttributes.Private | FieldAttributes.InitOnly);
                    eventBackingFields[evt] = backingField;

                    ctorIlgen.Emit(OpCodes.Ldarg_0);
                    ctorIlgen.Emit(OpCodes.Ldsfld, eventInfoField);

                    if (evt.EventHandlerType == typeof(EventHandler))
                    {
                        ctorIlgen.Emit(OpCodes.Ldsfld, ProcessProxyImplementation.Reflection.EventState_NonGenericCallbackField);
                    }
                    else if (evt.EventHandlerType.IsGenericType && evt.EventHandlerType.GetGenericTypeDefinition() == typeof(EventHandler<>))
                    {
                        ctorIlgen.Emit(OpCodes.Ldsfld, ProcessProxyImplementation.Reflection.GetEventState_GenericCallbackField(evt.EventHandlerType.GetGenericArguments().Single()));
                    }
                    else
                    {
                        throw new InvalidOperationException("TODO");
                    }

                    ctorIlgen.Emit(OpCodes.Newobj, typeof(ProcessProxyEventSubscriptionInfo).GetConstructors().Single());
                    ctorIlgen.Emit(OpCodes.Stfld, backingField);

                    ctorIlgen.Emit(OpCodes.Ldarg_0);
                    ctorIlgen.Emit(OpCodes.Ldarg_0);
                    ctorIlgen.Emit(OpCodes.Ldfld, backingField);
                    ctorIlgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.InitEventInfoMethod, null);

                    void EmitAddAndRemoveMethod(ILGenerator il, bool isAdd)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, backingField);
                        il.EmitCall(OpCodes.Callvirt, isAdd ? ProcessProxyImplementation.Reflection.AddEventSubscriptionMethod : ProcessProxyImplementation.Reflection.RemoveEventSubscriptionMethod, null);
                        il.Emit(OpCodes.Ret);
                    }

                    var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType);

                    var baseAddMethod = evt.GetAddMethod();
                    var addBuilder = typeBuilder.DefineExactOverride(baseAddMethod);
                    EmitAddAndRemoveMethod(addBuilder.GetILGenerator(), true);
                    eventBuilder.SetAddOnMethod(addBuilder);

                    var baseRemoveMethod = evt.GetRemoveMethod();
                    var removeBuilder = typeBuilder.DefineExactOverride(baseRemoveMethod);
                    EmitAddAndRemoveMethod(removeBuilder.GetILGenerator(), false);
                    eventBuilder.SetRemoveOnMethod(removeBuilder);
                }
            }

            ctorIlgen.Emit(OpCodes.Ret);

            var finalType = typeBuilder.CreateTypeInfo();
            var factoryMethod = finalType.FindUniqueMethod(factoryName);

            foreach (var m in methods)
            {
                var methodInfoField = finalType.GetField(methodInfoFieldNames[m], BindingFlags.NonPublic | BindingFlags.Static);
                methodInfoField.SetValue(null, new ReflectedMethodInfo(m, cacheVisitedTypes: true));
            }

            foreach(var evt in events)
            {
                var eventInfoField = finalType.GetField(GetEventInfoField(evt), BindingFlags.NonPublic | BindingFlags.Static);
                eventInfoField.SetValue(null, new ReflectedEventInfo(evt, cacheVisitedTypes: true));
            }
                       
            return (Func<ProcessProxyImplementation>)Delegate.CreateDelegate(typeof(Func<ProcessProxyImplementation>), factoryMethod);
        }
    }
}

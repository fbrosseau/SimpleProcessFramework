using Spfx.Utilities;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Runtime.Exceptions;

namespace Spfx.Runtime.Server
{
    internal static class ProcessEndpointHandlerFactory
    {
        private static class FactoryStorage<T>
        {
            internal static readonly ProcessEndpointHandlerFactoryDelegate Func = CreateFactory(ReflectionUtilities.GetType<T>());
        }

        public static IProcessEndpointHandler Create<T>(IProcessInternal parentProcess, string endpointId, T realTarget)
        {
            Guard.ArgumentNotNull(realTarget, nameof(realTarget));
            return FactoryStorage<T>.Func(parentProcess, endpointId, realTarget);
        }

        public static IProcessEndpointHandler Create(IProcessInternal parentProcess, string endpointId, object handler, Type interfaceType)
        {
            try
            {
                var createMethod = typeof(ProcessEndpointHandlerFactory).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "Create" && m.IsGenericMethod);
                return (IProcessEndpointHandler)createMethod.MakeGenericMethod(interfaceType).Invoke(null, new[] { parentProcess, endpointId, handler });
            }
            catch(Exception ex)
            {
                if (ex is TargetInvocationException && ex.InnerException != null)
                    ex = ex.InnerException;

                throw new CodeGenerationFailedException("ProcessEndpointHandlerFactory.Create failed for " + interfaceType.AssemblyQualifiedName, ex);
            }
        }

        private delegate ProcessEndpointHandler ProcessEndpointHandlerFactoryDelegate(IProcessInternal parentProcess, string endpointId, object target);
        
        private static ProcessEndpointHandlerFactoryDelegate CreateFactory(Type type)
        {
            Guard.ArgumentNotNull(type, nameof(type));

            if (!type.IsInterface)
                throw new ArgumentException("'T' must be an interface");

            DynamicCodeGenModule.IgnoreAccessChecks(type.Assembly);

            var typeBuilder = DynamicCodeGenModule.DynamicModule.DefineType("HandlerImpl__" + type.AssemblyQualifiedName,
                TypeAttributes.Public,
                typeof(ProcessEndpointHandler));

            var ctorArgs = new[] { typeof(IProcessInternal), typeof(string), typeof(object) };

            var factoryName = "Type Factory";
            var factoryMethodBuilder = typeBuilder.DefineMethod(factoryName,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                typeof(ProcessEndpointHandler),
                ctorArgs);

            var ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                ctorArgs);

            var ilgen = factoryMethodBuilder.GetILGenerator();
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldarg_1);
            ilgen.Emit(OpCodes.Ldarg_2);
            ilgen.Emit(OpCodes.Newobj, ctor);
            ilgen.Emit(OpCodes.Ret);

            var metadataFieldName = "__metadataDescriptor";

            var endpointMetadataField = typeBuilder.DefineField(
                metadataFieldName,
                typeof(ProcessEndpointDescriptor),
                FieldAttributes.Private | FieldAttributes.Static);

            var realImplField = typeBuilder.DefineField("__impl", type, FieldAttributes.Private | FieldAttributes.InitOnly);

            ilgen = ctor.GetILGenerator();
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldarg_1);
            ilgen.Emit(OpCodes.Ldarg_2);
            ilgen.Emit(OpCodes.Ldarg_3);
            ilgen.Emit(OpCodes.Ldsfld, endpointMetadataField);
            ilgen.Emit(OpCodes.Call, ProcessEndpointHandler.Reflection.Constructor);
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldarg_3);
            ilgen.Emit(OpCodes.Castclass, type);
            ilgen.Emit(OpCodes.Stfld, realImplField);

            static string GetFieldNameForEvent(EventInfo evt)
            {
                return "EventInfo__" + evt.Name;
            }

            EventInfo FindEvent(Type t, string name)
            {
                var evt = t.GetEvent(name);
                if (evt != null)
                    return evt;

                return t.GetInterfaces().Select(iface => FindEvent(iface, name)).FirstOrDefault(e => e != null);
            }

            var typeDescriptor = ProcessEndpointDescriptor.CreateFromCurrentProcess(type);


            var allEventsToImplement = new List<EventInfo>();
            foreach(var evtName in typeDescriptor.Events)
            {
                allEventsToImplement.Add(FindEvent(type, evtName));
            }

            foreach (var evt in allEventsToImplement)
            {
                var staticField = typeBuilder.DefineField(GetFieldNameForEvent(evt), typeof(ReflectedEventInfo), FieldAttributes.Private | FieldAttributes.Static);

                ilgen.Emit(OpCodes.Ldarg_0);
                ilgen.Emit(OpCodes.Ldsfld, staticField);
                ilgen.EmitCall(OpCodes.Callvirt, ProcessEndpointHandler.Reflection.PrepareEventMethod, null);
            }

            ilgen.Emit(OpCodes.Ret);

            var allMethods = typeDescriptor.Methods.Select(m => m.Method.ResolvedMethod).ToArray();

            var doRemoteCallImplDecl = ProcessEndpointHandler.Reflection.DoRemoteCallImplMethod;
            var doRemoteCallImplMethod = typeBuilder.DefineExactOverride(doRemoteCallImplDecl);

            ilgen = doRemoteCallImplMethod.GetILGenerator();

            var ldarg_this = OpCodes.Ldarg_0;
            var ldarg_callContext = OpCodes.Ldarg_1;

            var callRequestLocal = ilgen.DeclareLocal(typeof(IRemoteCallRequest));

            ilgen.Emit(ldarg_callContext);
            ilgen.EmitCall(OpCodes.Callvirt, InterprocessRequestContext.Reflection.Get_RequestMethod, null);
            ilgen.Emit(OpCodes.Castclass, typeof(RemoteCallRequest));
            ilgen.Emit(OpCodes.Stloc, callRequestLocal);

            var jumpLabels = new List<Label>();

            foreach (var m in allMethods)
            {
                jumpLabels.Add(ilgen.DefineLabel());
            }

            ilgen.Emit(ldarg_this);
            ilgen.Emit(OpCodes.Ldfld, realImplField);
            ilgen.Emit(OpCodes.Ldloc, callRequestLocal);
            ilgen.EmitCall(OpCodes.Callvirt, RemoteCallRequest.Reflection.Get_MethodIdMethod, null);
            ilgen.Emit(OpCodes.Conv_U);
            ilgen.Emit(OpCodes.Switch, jumpLabels.ToArray());

            var returnLabel = ilgen.DefineLabel();

            // fallthrough
            ilgen.Emit(OpCodes.Pop);
            ilgen.Emit(ldarg_this);
            ilgen.EmitCall(OpCodes.Callvirt, ProcessEndpointHandler.Reflection.ThrowBadInvocationWithoutTextMethod, null);
            ilgen.Emit(OpCodes.Br, returnLabel); // won't happen, this throws

            var taskLocals = new Dictionary<Type, LocalBuilder>();

            int i = 0;
            foreach (var m in allMethods)
            {
                ilgen.MarkLabel(jumpLabels[i++]);

                var retType = m.ReturnType;

                var methodArgs = m.GetParameters();
                for (int argI = 0; argI < methodArgs.Length; ++argI)
                {
                    var argInfo = methodArgs[argI];
                    if (argInfo.ParameterType == typeof(CancellationToken))
                    {
                        ilgen.Emit(ldarg_callContext);
                        ilgen.EmitCall(OpCodes.Callvirt, InterprocessRequestContext.Reflection.Get_CancellationMethod, null);
                    }
                    else
                    {
                        ilgen.Emit(OpCodes.Ldloc, callRequestLocal);
                        ilgen.Emit(OpCodes.Ldc_I4, argI);
                        ilgen.Emit(OpCodes.Callvirt, RemoteCallRequest.Reflection.GetArgMethod(argInfo.ParameterType));
                    }
                }

                ilgen.EmitCall(OpCodes.Callvirt, m, null);

                if (!taskLocals.TryGetValue(retType, out LocalBuilder returnLocal))
                    taskLocals[retType] = returnLocal = ilgen.DeclareLocal(retType);

                ilgen.Emit(OpCodes.Stloc, returnLocal);

                MethodInfo handlerMethod = null;

                if (retType == typeof(Task))
                {
                    handlerMethod = InterprocessRequestContext.Reflection.CompleteWithTaskMethod;
                }
                else if (retType == typeof(ValueTask))
                {
                    handlerMethod = InterprocessRequestContext.Reflection.CompleteWithValueTaskMethod;
                }
                else if (retType.IsGenericType)
                {
                    var baseGeneric = retType.GetGenericTypeDefinition();
                    if (baseGeneric == typeof(Task<>))
                    {
                        handlerMethod = InterprocessRequestContext.Reflection.GetCompleteWithTaskOfTMethod(retType.GetGenericArguments()[0]);
                    }
                    else if (baseGeneric == typeof(ValueTask<>))
                    {
                        handlerMethod = InterprocessRequestContext.Reflection.GetCompleteWithValueTaskOfTMethod(retType.GetGenericArguments()[0]);
                    }
                }

                if (handlerMethod == null)
                {
                    throw new InvalidOperationException("Return type of method " + m.Name + " is not handled");
                }

                ilgen.Emit(ldarg_callContext);
                ilgen.Emit(OpCodes.Ldloc, returnLocal);
                ilgen.EmitCall(OpCodes.Callvirt, handlerMethod, null);
                ilgen.Emit(OpCodes.Br, returnLabel);
            }

            ilgen.MarkLabel(returnLabel);
            ilgen.Emit(OpCodes.Ret);

            var finalType = typeBuilder.CreateTypeInfo();
            var factoryMethod = finalType.FindUniqueMethod(factoryName);

            var finalMetadataField = finalType.GetField(metadataFieldName, BindingFlags.NonPublic | BindingFlags.Static);
            finalMetadataField.SetValue(null, typeDescriptor);

            foreach (var evt in allEventsToImplement)
            {
                var staticField = finalType.GetField(GetFieldNameForEvent(evt), BindingFlags.NonPublic | BindingFlags.Static);
                staticField.SetValue(null, new ReflectedEventInfo(evt));
            }

            return (ProcessEndpointHandlerFactoryDelegate)Delegate.CreateDelegate(typeof(ProcessEndpointHandlerFactoryDelegate), factoryMethod);
        }
    }
}
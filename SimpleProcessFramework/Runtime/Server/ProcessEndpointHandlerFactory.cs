using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal static class ProcessEndpointHandlerFactory
    {
        private static class FactoryStorage<T>
        {
            internal static readonly Func<object, ProcessEndpointHandler> Func;

            static FactoryStorage()
            {
                Func = CreateFactory(typeof(T));
            }
        }

        public static IProcessEndpointHandler Create<T>(T realTarget)
        {
            Guard.ArgumentNotNull(realTarget, nameof(realTarget));

            var handler = FactoryStorage<T>.Func(realTarget);
            return handler;
        }

        private static Func<object, ProcessEndpointHandler> CreateFactory(Type type)
        {
            Guard.ArgumentNotNull(type, nameof(type));

            if (!type.IsInterface)
                throw new ArgumentException("'T' must be an interface");

            var typeBuilder = DynamicCodeGenModule.DynamicModule.DefineType("HandlerImpl__" + type.AssemblyQualifiedName,
                TypeAttributes.Public,
                typeof(ProcessEndpointHandler));

            var factoryName = "Type Factory";
            var factoryMethodBuilder = typeBuilder.DefineMethod(factoryName,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                typeof(ProcessEndpointHandler),
                new[] { typeof(object) });

            var ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(object) });

            var ilgen = factoryMethodBuilder.GetILGenerator();
            ilgen.Emit(OpCodes.Ldarg_0);
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
            ilgen.Emit(OpCodes.Ldsfld, endpointMetadataField);
            ilgen.Emit(OpCodes.Call, typeof(ProcessEndpointHandler).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single());
            ilgen.Emit(OpCodes.Ldarg_0);
            ilgen.Emit(OpCodes.Ldarg_1);
            ilgen.Emit(OpCodes.Castclass, type);
            ilgen.Emit(OpCodes.Stfld, realImplField);
            ilgen.Emit(OpCodes.Ret);

            var allMethods = type.GetMethods();

            var doRemoteCallImplDecl = ProcessEndpointHandler.Reflection.DoRemoteCallImplMethod;
            var doRemoteCallImplMethod = typeBuilder.DefineExactOverride(doRemoteCallImplDecl);

            ilgen = doRemoteCallImplMethod.GetILGenerator();

            var rawArgsArrayLocal = ilgen.DeclareLocal(typeof(object[]));

            var ldarg_this = OpCodes.Ldarg_0;
            var ldarg_callContext = OpCodes.Ldarg_1;

            var callRequestLocal = ilgen.DeclareLocal(typeof(RemoteCallRequest));

            ilgen.Emit(ldarg_callContext);
            ilgen.EmitCall(OpCodes.Callvirt, InterprocessRequestContext.Reflection.Get_RequestMethod, null);
            ilgen.Emit(OpCodes.Castclass, typeof(RemoteCallRequest));
            ilgen.Emit(OpCodes.Stloc, callRequestLocal);
            ilgen.Emit(OpCodes.Ldloc, callRequestLocal);
            ilgen.EmitCall(OpCodes.Callvirt, RemoteCallRequest.Reflection.GetArgsOrEmptyMethod, null);
            ilgen.Emit(OpCodes.Stloc, rawArgsArrayLocal);

            var jumpLabels = new List<Label>();

            var methodDescriptors = new List<ProcessEndpointMethodDescriptor>();
            foreach (var m in allMethods)
            {
                methodDescriptors.Add(new ProcessEndpointMethodDescriptor
                {
                    Method = new ReflectedMethodInfo(m),
                    MethodId = methodDescriptors.Count,
                    IsCancellable = m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken))
                });

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
            ilgen.EmitCall(OpCodes.Callvirt, ProcessEndpointHandler.Reflection.ThrowBadInvocationMethod, null);
            ilgen.Emit(OpCodes.Br, returnLabel); // won't happen, this throws

            var taskLocals = new Dictionary<Type, LocalBuilder>();

            int i = 0;
            foreach (var m in allMethods)
            {
                ilgen.MarkLabel(jumpLabels[i++]);

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
                        ilgen.Emit(OpCodes.Ldloc, rawArgsArrayLocal);
                        ilgen.Emit(OpCodes.Ldc_I4, argI);
                        ilgen.Emit(OpCodes.Ldelem, typeof(object));

                        if (argInfo.ParameterType.IsValueType)
                        {
                            ilgen.Emit(OpCodes.Unbox_Any, argInfo.ParameterType);
                        }
                        else
                        {
                            ilgen.Emit(OpCodes.Castclass, argInfo.ParameterType);
                        }
                    }
                }

                ilgen.EmitCall(OpCodes.Callvirt, m, null);

                if (typeof(Task).IsAssignableFrom(m.ReturnType))
                {
                    if (!taskLocals.TryGetValue(m.ReturnType, out LocalBuilder taskLocal))
                        taskLocals[m.ReturnType] = taskLocal = ilgen.DeclareLocal(m.ReturnType);

                    ilgen.Emit(OpCodes.Stloc, taskLocal);
                    ilgen.Emit(ldarg_callContext);
                    ilgen.Emit(OpCodes.Ldloc, taskLocal);
                    if (m.ReturnType == typeof(Task))
                    {
                        ilgen.EmitCall(OpCodes.Callvirt, InterprocessRequestContext.Reflection.CompleteWithTaskMethod, null);
                    }
                    else
                    {
                        ilgen.EmitCall(OpCodes.Callvirt, InterprocessRequestContext.Reflection.GetCompleteWithTaskOfTMethod(m.ReturnType.GetGenericArguments()[0]), null);
                    }
                    ilgen.Emit(OpCodes.Br, returnLabel);
                }
            }

            ilgen.MarkLabel(returnLabel);
            ilgen.Emit(OpCodes.Ret);

            var finalType = typeBuilder.CreateTypeInfo();
            var factoryMethod = finalType.FindUniqueMethod(factoryName);

            var finalMetadataField = finalType.GetField(metadataFieldName, BindingFlags.NonPublic | BindingFlags.Static);
            finalMetadataField.SetValue(null, new ProcessEndpointDescriptor
            {
                Methods = methodDescriptors.ToArray()
            });

            return (Func<object, ProcessEndpointHandler>)Delegate.CreateDelegate(typeof(Func<object, ProcessEndpointHandler>), factoryMethod);
        }
    }
}
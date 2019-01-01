using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    internal static class ProcessProxyFactory
    {
        private static class FactoryStorage<T>
        {
            internal static readonly Func<ProcessProxyImplementation> Func;

            static FactoryStorage()
            {
                Func = CreateFactory(typeof(T));
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

            var ctorBuilder = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            var ilgen = factoryMethodBuilder.GetILGenerator();
            ilgen.Emit(OpCodes.Newobj, ctorBuilder);
            ilgen.Emit(OpCodes.Ret);

            var methodInfoFieldNames = new Dictionary<MethodInfo, string>();

            foreach (var m in type.GetMethods())
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
                    FieldAttributes.Public | FieldAttributes.Static);

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
                            if (argType.IsValueType)
                            {
                                ilgen.EmitCall(OpCodes.Call, BoxHelper.Reflection.GetBoxMethod(argType), null);
                            }

                            if (argType.IsByRef)
                                throw new InvalidProxyInterfaceException("Method " + m.Name + " of type " + type.AssemblyQualifiedName + " cannot have ref/out parameters");
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

                if (typeof(Task).IsAssignableFrom(m.ReturnType))
                {
                    ilgen.Emit(OpCodes.Ldloc, cancellationTokenLocal);

                    if (m.ReturnType == typeof(Task))
                    {
                        ilgen.Emit(OpCodes.Tailcall);
                        ilgen.EmitCall(OpCodes.Callvirt, ProcessProxyImplementation.Reflection.WrapTaskReturnMethod, null);
                    }
                    else
                    {
                        var taskResultType = m.ReturnType.GetGenericArguments()[0];
                        var genericTaskMethod = ProcessProxyImplementation.Reflection.WrapGenericTaskReturnMethod;
                        var taskMethod = genericTaskMethod.MakeGenericMethod(taskResultType);
                        ilgen.Emit(OpCodes.Tailcall);
                        ilgen.EmitCall(OpCodes.Callvirt, genericTaskMethod, null);
                    }
                }

                ilgen.Emit(OpCodes.Ret);
            }

            var finalType = typeBuilder.CreateTypeInfo();
            var factoryMethod = finalType.FindUniqueMethod(factoryName);

            foreach (var m in type.GetMethods())
            {
                var methodInfoField = finalType.GetField(methodInfoFieldNames[m], BindingFlags.Public | BindingFlags.Static);
                methodInfoField.SetValue(null, new ReflectedMethodInfo(m));
            }
                       
            return (Func<ProcessProxyImplementation>)Delegate.CreateDelegate(typeof(Func<ProcessProxyImplementation>), factoryMethod);
        }
    }
}

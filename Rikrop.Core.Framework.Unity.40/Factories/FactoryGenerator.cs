using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Practices.Unity;

namespace Rikrop.Core.Framework.Unity.Factories
{
    /// <summary>
    ///     Generates factory.
    /// </summary>
    internal class FactoryGenerator
    {
        private static TypeInfo DefineType(ModuleBuilder moduleBuilder, Type factoryInterfaceType)
        {
            var typeName = "ImplementationOf" + factoryInterfaceType.FullName;
            const TypeAttributes typeAttr = TypeAttributes.Public |
                                            TypeAttributes.Class |
                                            TypeAttributes.AutoClass |
                                            TypeAttributes.AnsiClass |
                                            TypeAttributes.BeforeFieldInit;
            var typeBuilder = moduleBuilder.DefineType(typeName, typeAttr, typeof (object),
                                                       new[] {factoryInterfaceType});
            // Define: private readonly IUnityContainer _unityContainer;
            var unityContainerField = typeBuilder.DefineField("_unityContainer", typeof (IUnityContainer),
                                                              FieldAttributes.Private |
                                                              FieldAttributes.InitOnly);

            return new TypeInfo(typeBuilder, factoryInterfaceType, unityContainerField);
        }

        private static void EmitLdarg(ILGenerator ilGenerator, int index)
        {
            switch (index)
            {
                case 0:
                    ilGenerator.Emit(OpCodes.Ldarg_1);
                    break;
                case 1:
                    ilGenerator.Emit(OpCodes.Ldarg_2);
                    break;
                case 2:
                    ilGenerator.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    ilGenerator.Emit(OpCodes.Ldarg_S, index + 1);
                    break;
            }
        }

        private static ConstructorInfo FindTargetTypeConstructor(Type targetType)
        {
            if (targetType.IsAbstract || targetType.IsInterface)
            {
                throw new FactoryGeneratorException(
                    string.Format("Method {0} should return concrete class.", targetType.FullName));
            }

            var targetTypeConstructors = targetType.GetConstructors();
            if (targetTypeConstructors.Length == 0)
            {
                throw new FactoryGeneratorException(
                    string.Format("The type {0} does not have an accessible constructor.", targetType.FullName));
            }

            if (targetTypeConstructors.Length > 1)
            {
                throw new FactoryGeneratorException(
                    string.Format("The type {0} has multiple constructors.", targetType.FullName));
            }

            return targetTypeConstructors[0];
        }

        private static MethodInfo FindUnityResolveMethod()
        {
            // Look for method:
            // public static T Resolve<T>(this IUnityContainer container, params ResolverOverride[] overrides);
            var resolveMethodInfo = typeof (UnityContainerExtensions)
                .GetMethods()
                .FirstOrDefault(m =>
                                m.IsStatic &&
                                m.Name == "Resolve" &&
                                m.IsGenericMethod &&
                                m.GetGenericArguments().Length == 1 &&
                                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[]
                                                                                                 {
                                                                                                     typeof (IUnityContainer),
                                                                                                     typeof (ResolverOverride[])
                                                                                                 }));

            if (resolveMethodInfo == null)
            {
                throw new FactoryGeneratorException("Can't find method Resolve<T>() in class UnityContainerExtensions");
            }

            return resolveMethodInfo;
        }

        private static void GenerateConstructor(TypeInfo typeInfo)
        {
            // Define: public ConstructorName(IUnityContainer unityContainer)
            var constructorBuilder = typeInfo.TypeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] {typeof (IUnityContainer)});

            var il = constructorBuilder.GetILGenerator();

            var afterArgumentCheckLabel = il.DefineLabel();

            // Emit call constructor of the supertype (Object)
            il.Emit(OpCodes.Ldarg_0);
            var superTypeConstructorInfo = typeof (object).GetConstructor(new Type[0]);
            // ReSharper disable AssignNullToNotNullAttribute
            il.Emit(OpCodes.Call, superTypeConstructorInfo);
            // ReSharper restore AssignNullToNotNullAttribute            

            // Emit: if (_unityContainer == null)
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brtrue_S, afterArgumentCheckLabel);

            // Emit: throw new ArgumentNullException("unityContainer");
            il.Emit(OpCodes.Ldstr, "unityContainer");
            var exceptionConstructorInfo =
                typeof (ArgumentNullException).GetConstructor(new[] {typeof (string)});
            // ReSharper disable AssignNullToNotNullAttribute
            il.Emit(OpCodes.Newobj, exceptionConstructorInfo);
            // ReSharper restore AssignNullToNotNullAttribute
            il.Emit(OpCodes.Throw);

            // Emit: _unityContainer = unityContainer;
            il.MarkLabel(afterArgumentCheckLabel);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, typeInfo.UnityContainerField);

            il.Emit(OpCodes.Ret);
        }

        private static bool TryGetTargetTargetTypeFromMemberAttribute(MethodInfo methodInfo, out Type targetType)
        {
            var createAttribute = methodInfo.GetCustomAttribute<CreatesAttribute>();

            if (createAttribute != null)
            {
                if (IsImplementingInterface(createAttribute.TypeToCreate, interfaceType: methodInfo.ReturnType))
                {
                    targetType = createAttribute.TypeToCreate;
                    return true;
                }
            }

            targetType = null;
            return false;
        }

        private static bool IsImplementingInterface(Type typeToCreate, Type interfaceType)
        {
            return typeToCreate.GetInterfaces().Contains(interfaceType);
        }

        private static void GenerateCreateMethods(TypeInfo typeInfo, MethodInfo resolveMethodInfo)
        {
            // Generate code like this:
            // public SomeClass Create(int intPrm)
            // {
            //     return new SomeClass(
            //         ResolveParameterWithException<ILogger>("logger"),
            //         intPrm);
            // }

            foreach (var factoryMethodInfo in typeInfo.FactoryInterfaceType.GetMethods())
            {
                var targetType = GetTargetType(factoryMethodInfo);

                var targetTypeConstructor = FindTargetTypeConstructor(targetType);

                var methodParametersInfo = factoryMethodInfo.GetParameters();

                var methodBuilder = typeInfo.TypeBuilder.DefineMethod(factoryMethodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis,
                                                                      factoryMethodInfo.ReturnType, methodParametersInfo.Select(p => p.ParameterType).ToArray());

                var il = methodBuilder.GetILGenerator();

                var mapper = new Mapper(methodParametersInfo);

                var factoryMethodParameterIndex = 0;
                foreach (var targetTypeConstructorPrmInfo in targetTypeConstructor.GetParameters())
                {
                    var factoryMethodParameterInfo = mapper.FindMethodParameterForConstructorParameter(targetTypeConstructorPrmInfo);
                    if (factoryMethodParameterInfo == null)
                    {
                        // resolve constructor parameter from container
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, targetTypeConstructorPrmInfo.Name);
                        il.Emit(OpCodes.Call, resolveMethodInfo.MakeGenericMethod(targetTypeConstructorPrmInfo.ParameterType));
                    }
                    else
                    {
                        // pass parameter of generated method to constructor
                        EmitLdarg(il, factoryMethodParameterIndex);
                        factoryMethodParameterIndex++;
                    }
                }

                il.Emit(OpCodes.Newobj, targetTypeConstructor);
                il.Emit(OpCodes.Ret);
            }
        }

        private static Type GetTargetType(MethodInfo factoryMethodInfo)
        {
            Type targetType;

            if (factoryMethodInfo.ReturnType.IsInterface)
            {
                if (!TryGetTargetTargetTypeFromMemberAttribute(factoryMethodInfo, out targetType))
                {
                    throw new FactoryGeneratorException("Return type is interface and method does not contains CreatesAttribute, or CreateAtttibute type is not derived from method's return type");
                }
            }
            else
            {
                targetType = factoryMethodInfo.ReturnType;
            }

            return targetType;
        }

        private static MethodInfo GenerateResolveParameterWithExceptionMethod(TypeInfo typeInfo)
        {
//          Generate method like this:
//          private T ResolveParameterWithException<T>(string parameterName)
//          {
//              try
//              {
//                  return _container.Resolve<T>();
//              }
//              catch (Exception exception)
//              {
//                  throw new Exception(
//                      string.Format("Can't create instance of type {0}, can't resolve parameter {1}.", typeof(T).FullName, parameterName),
//                      exception);
//              }
//          }

            var methodBuilder = typeInfo.TypeBuilder.DefineMethod(
                "ResolveParameterWithException",
                MethodAttributes.Private, CallingConventions.HasThis);

            methodBuilder.SetParameters(typeof (int));

            var genericTypeParameterBuilder = methodBuilder.DefineGenericParameters("T")[0];
            methodBuilder.SetReturnType(genericTypeParameterBuilder);

            var il = methodBuilder.GetILGenerator();

            var endExceptionBlockLabel = il.DefineLabel();

            il.DeclareLocal(typeof (Exception));
            il.DeclareLocal(genericTypeParameterBuilder);

            il.BeginExceptionBlock();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeInfo.UnityContainerField);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Newarr, typeof (ResolverOverride));
            var resolveMethodInfo = FindUnityResolveMethod();
            il.Emit(OpCodes.Call,
                    resolveMethodInfo.MakeGenericMethod(genericTypeParameterBuilder));
            il.Emit(OpCodes.Stloc_1);
            il.Emit(OpCodes.Leave_S, endExceptionBlockLabel);

            il.BeginCatchBlock(typeof (Exception));

            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldstr, "Can't create instance of type {0}, сan't resolve parameter {1}.");
            il.Emit(OpCodes.Ldtoken, genericTypeParameterBuilder);
            il.Emit(OpCodes.Call, typeof (Type).GetMethod("GetTypeFromHandle", new[] {typeof (RuntimeTypeHandle)}));
            il.Emit(OpCodes.Callvirt, typeof (Type).GetProperty("FullName").GetGetMethod());
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof (string).GetMethod("Format", new[] {typeof (string), typeof (object), typeof (object)}));
            il.Emit(OpCodes.Ldloc_0);
            // ReSharper disable AssignNullToNotNullAttribute
            il.Emit(OpCodes.Newobj, typeof (Exception).GetConstructor(new[] {typeof (string), typeof (CreateInstanceException)}));
            // ReSharper restore AssignNullToNotNullAttribute
            il.Emit(OpCodes.Throw);

            il.EndExceptionBlock();

            il.MarkLabel(endExceptionBlockLabel);

            il.Emit(OpCodes.Ldloc_1);
            il.Emit(OpCodes.Ret);
            return methodBuilder;
        }

        public Type Generate(Type factoryInterfaceType)
        {
            if (!factoryInterfaceType.IsInterface)
            {
                throw new FactoryGeneratorException(
                    string.Format("Found class {0}, but interface was expected.", factoryInterfaceType.Name));
            }
            var assemblyName = new AssemblyName("GeneratedTypedFactories");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,
                                                                                AssemblyBuilderAccess
                                                                                    .RunAndCollect);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("GeneratedTypedFactories");
            var typeInfo = DefineType(moduleBuilder, factoryInterfaceType);
            GenerateConstructor(typeInfo);
            var resolveMi = GenerateResolveParameterWithExceptionMethod(typeInfo);
            GenerateCreateMethods(typeInfo, resolveMi);
            return typeInfo.TypeBuilder.CreateType();
        }

        private class TypeInfo
        {
            private readonly Type _factoryInterfaceType;
            private readonly TypeBuilder _typeBuilder;
            private readonly FieldBuilder _unityContainerField;

            public Type FactoryInterfaceType
            {
                get { return _factoryInterfaceType; }
            }

            public TypeBuilder TypeBuilder
            {
                get { return _typeBuilder; }
            }

            public FieldBuilder UnityContainerField
            {
                get { return _unityContainerField; }
            }

            public TypeInfo(TypeBuilder typeBuilder, Type factoryInterfaceType, FieldBuilder unityContainerField)
            {
                _typeBuilder = typeBuilder;
                _factoryInterfaceType = factoryInterfaceType;
                _unityContainerField = unityContainerField;
            }
        }
    }
}
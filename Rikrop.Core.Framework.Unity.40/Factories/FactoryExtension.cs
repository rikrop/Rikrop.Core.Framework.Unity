using System;
using System.Linq;
using System.Reflection;
using Microsoft.Practices.Unity;

namespace Rikrop.Core.Framework.Unity.Factories
{
    /// <summary>
    /// Extension methods for providing custom typed factories based on a factory interface.
    /// </summary>
    public static class FactoryExtension
    {
        /// <summary>
        /// Implement and register factory.
        /// </summary>
        /// <typeparam name="TFactory">Factory interface type.</typeparam>
        /// <param name="container">Unity container.</param>
        public static void RegisterFactory<TFactory>(this IUnityContainer container)
        {
            RegisterFactory(container, typeof (TFactory));
        }

        /// <summary>
        /// Implement and register factory.
        /// </summary>
        /// <param name="container">Unity container.</param>
        /// <param name="factoryInterfaceType">Factory interface type.</param>
        public static void RegisterFactory(this IUnityContainer container, Type factoryInterfaceType)
        {
            var factoryGenerator = new FactoryGenerator();
            var concreteFactoryType = factoryGenerator.Generate(factoryInterfaceType);
            container.RegisterType(
                factoryInterfaceType,
                new InjectionFactory(
                    c =>
                        {
                            var activator = new FactoryActivator();
                            return activator.CreateInstance(c, concreteFactoryType);
                        }));
        }


        /// <summary>
        /// Implement and register factory.
        /// </summary>
        /// <param name="container">Unity container.</param>
        /// <param name="assembly">Assembly that contains auto factory interfaces, marked with attribute AutoFactoryAttribute.</param>
        public static void RegisterAutoFactories(this IUnityContainer container, Assembly assembly)
        {
            foreach (var interfaceType in assembly.GetTypes().Where(type => type.IsInterface))
            {
                if (interfaceType.GetCustomAttribute<AutoFactoryAttribute>() != null)
                {
                    container.RegisterFactory(interfaceType);
                }
            }
        }
    }
}


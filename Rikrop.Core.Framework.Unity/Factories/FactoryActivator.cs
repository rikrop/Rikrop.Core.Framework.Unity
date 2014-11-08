using System;
using Microsoft.Practices.Unity;

namespace Rikrop.Core.Framework.Unity.Factories
{
    /// <summary>
    /// Calls FactoryGenerator and creates instance of factory.
    /// </summary>
    internal class FactoryActivator
    {
        public object CreateInstance(IUnityContainer container, Type concreteFactoryType)
        {
            var constructor = concreteFactoryType.GetConstructor(new[] {typeof (IUnityContainer)});
            if (constructor == null)
            {
                throw new FactoryGeneratorException(
                    "Generated factory doesn't have public constructor with IUnityContainer parameter. " +
                    "There's something wrong with FactoryGenerator.");
            }

            return constructor.Invoke(new object[] {container});
        }
    }
}
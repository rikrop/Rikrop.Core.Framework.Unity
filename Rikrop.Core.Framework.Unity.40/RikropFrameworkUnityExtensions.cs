using Microsoft.Practices.Unity;

namespace Rikrop.Core.Framework.Unity
{
    public static class RikropFrameworkUnityExtensions
    {
        /// <summary>
        ///   ������������ TodayNowProvider � DateTimeProvider.
        /// </summary>
        public static IUnityContainer RegisterDateTimeProviders(this IUnityContainer container)
        {
            container.RegisterType<ITodayNowProvider, TodayNowProvider>(new ContainerControlledLifetimeManager());
            container.RegisterType<IDateTimeProvider, DateTimeProvider>(new ContainerControlledLifetimeManager());

            return container;
        }
    }
}
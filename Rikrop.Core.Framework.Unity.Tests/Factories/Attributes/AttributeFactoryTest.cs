using Rikrop.Core.Framework.Unity.Factories;
using Microsoft.Practices.Unity;
using NUnit.Framework;

namespace Rikrop.Core.Framework.Unity.Tests.Factories.Attributes
{
    [TestFixture]
    internal class AttributeFactoryTest
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            _container = new UnityContainer();
            _container.RegisterType<Stubs.INonVolatileDependency, Stubs.NonVolatileClass>();
        }

        #endregion

        private IUnityContainer _container;

        [Test]
        public void MustRegisterInterfacesWithAutoFactoryAttribute()
        {
            _container.RegisterAutoFactories(typeof (AttributeFactoryTest).Assembly);
            Assert.DoesNotThrow(() => _container.Resolve<Stubs.ClassThatUsesFactory>());
        }

        [Test]
        public void ShouldCreateTypeFromAttributeIfMethodHasCreateAttribute()
        {
            _container.RegisterFactory(typeof (Stubs.IAutoFactory));
            Assert.DoesNotThrow(() => _container.Resolve<Stubs.ClassThatUsesFactory>());
        }

        [Test]
        public void MustThrowExceptionIfTypeFromCreateAttributeIsNotImplementingInterfaceInMethodsReturnType()
        {
            Assert.Throws<FactoryGeneratorException>(() =>_container.RegisterFactory(typeof(Stubs.IBrokenAutoFactory)));
        }
    }
}
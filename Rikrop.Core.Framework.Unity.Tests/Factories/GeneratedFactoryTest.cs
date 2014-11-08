using System;
using System.IO;
using Rikrop.Core.Framework.Unity.Factories;
using Microsoft.Practices.Unity;
using NUnit.Framework;
using Rhino.Mocks;

namespace Rikrop.Core.Framework.Unity.Tests.Factories
{
    [TestFixture]
    public class GeneratedFactoryTest
    {
        public class ConcreteClass
        {
            public interface IFactory
            {
                ConcreteClass Create();
            }
        }

        public class ConcreteClassWithParameter
        {
            public ConcreteClassWithParameter(int i)
            {
                Assert.AreEqual(100, i);
            }

            public interface IFactory
            {
                ConcreteClassWithParameter Create(int i);
            }
        }

        public class ConcreteClassWithDependencyFromIoC
        {
            public ConcreteClassWithDependencyFromIoC(Stream stream)
            {
                Assert.IsInstanceOf<MemoryStream>(stream);
            }

            public interface IDefectiveFactory
            {
                ConcreteClassWithDependencyFromIoC Create();
            }

            public interface IFactory
            {
                ConcreteClassWithDependencyFromIoC Create(int i);
            }
        }

        private static TFactory ImplementFactory<TFactory>(IUnityContainer container)
        {
            var factoryGenerator = new FactoryGenerator();
            var concreteFactoryType = factoryGenerator.Generate(typeof (TFactory));
            var activator = new FactoryActivator();
            return (TFactory) activator.CreateInstance(container, concreteFactoryType);
        }

        [Test]
        public void ShouldCreateFactoryThatCreatesConcreteClass()
        {
            var container = MockRepository.GenerateStrictMock<IUnityContainer>();

            var factory = ImplementFactory<ConcreteClass.IFactory>(container);
            var result = factory.Create();

            Assert.NotNull(result);
        }

        [Test]
        public void ShouldCreateFactoryThatCreatesConcreteClassWithDependencyFromIoC()
        {
            var container = MockRepository.GenerateStrictMock<IUnityContainer>();
            container.Stub(a => a.Resolve(typeof (Stream))).Return(new MemoryStream());

            var factory = ImplementFactory<ConcreteClassWithDependencyFromIoC.IFactory>(container);
            var result = factory.Create(100);

            Assert.NotNull(result);
        }

        [Test]
        public void ShouldCreateFactoryThatCreatesConcreteClassWithParameter()
        {
            var container = MockRepository.GenerateStrictMock<IUnityContainer>();

            var factory = ImplementFactory<ConcreteClassWithParameter.IFactory>(container);
            var result = factory.Create(100);

            Assert.NotNull(result);
        }

        [Test]
        public void ShouldCreateFactoryThatWrapsUnityExceptionIfCantResolveConstructorParameter()
        {
            var container = MockRepository.GenerateStrictMock<IUnityContainer>();
            var unityException = new Exception("Some unity exception.");
            container.Stub(a => a.Resolve(typeof (Stream))).Throw(unityException);

            var factory = ImplementFactory<ConcreteClassWithDependencyFromIoC.IDefectiveFactory>(container);

            try
            {
                factory.Create();
                Assert.Fail("Expected an exception.");
            }
            catch (Exception exception)
            {
                Assert.That(exception, Is.TypeOf(typeof (Exception)));
                Assert.AreEqual(
                    "Can't create instance of type System.IO.Stream, сan't resolve parameter stream.",
                    exception.Message);
                Assert.AreEqual(unityException, exception.InnerException);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rikrop.Core.Framework.Unity.Factories;
using NUnit.Framework;

namespace Rikrop.Core.Framework.Unity.Tests.Factories
{
    [TestFixture]
    public class MapperTest
    {
        private class MockParameterInfo : ParameterInfo
        {
            public MockParameterInfo(Type type)
            {
                ClassImpl = type;
            }
        }

        private static IEnumerable<ParameterInfo> Map(
            IEnumerable<ParameterInfo> factoryMethodParametersInfo,
            IEnumerable<ParameterInfo> constructorParametersInfo)
        {
            var mapper = new Mapper(factoryMethodParametersInfo);
            return constructorParametersInfo.Select(mapper.FindMethodParameterForConstructorParameter);
        }

        [Test]
        public void ShouldMap()
        {
            var factoryMethodParametersInfo = new[]
                {
                    new MockParameterInfo(typeof (int)),
                    new MockParameterInfo(typeof (Stream))
                };

            var constructorParametersInfo = new[]
                {
                    new MockParameterInfo(typeof (int)),
                    new MockParameterInfo(typeof (long)),
                    new MockParameterInfo(typeof (Stream))
                };

            var result = Map(factoryMethodParametersInfo, constructorParametersInfo);

            Assert.That(
                new ParameterInfo[]
                    {
                        factoryMethodParametersInfo[0],
                        null,
                        factoryMethodParametersInfo[1]
                    },
                Is.EqualTo(result));
        }

        [Test]
        public void ShouldMapIfFactoryMethodHasNoParameters()
        {
            var factoryMethodParametersInfo = new MockParameterInfo[] {};

            var constructorParametersInfo = new[]
                {
                    new MockParameterInfo(typeof (int)),
                    new MockParameterInfo(typeof (Stream))
                };

            var result = Map(factoryMethodParametersInfo, constructorParametersInfo);

            Assert.That(
                new ParameterInfo[]
                    {
                        null,
                        null
                    },
                Is.EqualTo(result));
        }

        [Test]
        public void ShouldntMapAnotherConstructorParameterOfSameType()
        {
            var factoryMethodParametersInfo = new[]
                {
                    new MockParameterInfo(typeof (int)),
                };

            var constructorParametersInfo = new[]
                {
                    new MockParameterInfo(typeof (int)),
                    new MockParameterInfo(typeof (int))
                };

            var result = Map(factoryMethodParametersInfo, constructorParametersInfo);

            Assert.That(
                new ParameterInfo[]
                    {
                        factoryMethodParametersInfo[0],
                        null
                    },
                Is.EqualTo(result));
        }
    }
}
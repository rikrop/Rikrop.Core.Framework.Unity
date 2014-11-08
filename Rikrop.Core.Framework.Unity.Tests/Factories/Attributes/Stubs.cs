using Rikrop.Core.Framework.Unity.Factories;

namespace Rikrop.Core.Framework.Unity.Tests.Factories.Attributes
{
    public static class Stubs
    {
        public class ClassThatUsesFactory
        {
            private readonly IAutoFactory _autoFactory;
            private IVolatileDependency _volatileDependency;

            public ClassThatUsesFactory(IAutoFactory autoFactory)
            {
                _autoFactory = autoFactory;
                _volatileDependency = autoFactory.Create(10);
            }
        }

        [AutoFactory]
        public interface IAutoFactory
        {
            [Creates(typeof(VolatileClass))]
            IVolatileDependency Create(int value);
        }

        public interface IBrokenAutoFactory
        {
            [Creates(typeof(string))]
            IVolatileDependency Create(int value);
        }

        public interface INonVolatileDependency
        {
        }

        public interface IVolatileDependency
        {
            int GetValue();
        }

        public class NonVolatileClass : INonVolatileDependency
        {
        }

        public class VolatileClass : IVolatileDependency
        {
            private readonly int _someVolatileParameter;
            private readonly INonVolatileDependency _nonVolatileDependency;

            public VolatileClass(int someVolatileParameter, INonVolatileDependency nonVolatileDependency)
            {
                _someVolatileParameter = someVolatileParameter;
                _nonVolatileDependency = nonVolatileDependency;
            }

            public int GetValue()
            {
                return _someVolatileParameter;
            }
        }
    }
}
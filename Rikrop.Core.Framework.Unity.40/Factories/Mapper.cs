using System.Collections.Generic;
using System.Reflection;

namespace Rikrop.Core.Framework.Unity.Factories
{
    /// <summary>
    /// Implements mapping between parameters of factory method and parameters of constructor.
    /// </summary>
    internal class Mapper
    {
        private ParameterInfo _candidate;
        private readonly IEnumerator<ParameterInfo> _factoryMethodParameterEnumerator;

        public Mapper(IEnumerable<ParameterInfo> factoryMethodParametersInfo)
        {
            _factoryMethodParameterEnumerator = factoryMethodParametersInfo.GetEnumerator();
        }

        public ParameterInfo FindMethodParameterForConstructorParameter(
            ParameterInfo constructorParameterInfo)
        {
            if (_candidate == null && _factoryMethodParameterEnumerator.MoveNext())
            {
                _candidate = _factoryMethodParameterEnumerator.Current;
            }

            if (_candidate != null && _candidate.ParameterType == constructorParameterInfo.ParameterType)
            {
                var result = _candidate;
                _candidate = null;
                return result;
            }

            return null;
        }
    }
}
using System;
using System.Runtime.Serialization;

namespace Rikrop.Core.Framework.Unity.Factories
{
    [Serializable]
    public class FactoryGeneratorException : Exception
    {
        public FactoryGeneratorException()
        {
        }

        public FactoryGeneratorException(string message)
            : base(message)
        {
        }

        public FactoryGeneratorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected FactoryGeneratorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
using System;
using System.Runtime.Serialization;

namespace Rikrop.Core.Framework.Unity.Factories
{
    [Serializable]
    public class CreateInstanceException : Exception
    {
        public CreateInstanceException()
        {
        }

        public CreateInstanceException(string message)
            : base(message)
        {
        }

        public CreateInstanceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected CreateInstanceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
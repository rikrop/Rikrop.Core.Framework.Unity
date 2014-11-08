using System;

namespace Rikrop.Core.Framework.Unity.Factories
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CreatesAttribute : Attribute
    {
        private readonly Type _typeToCreate;

        public Type TypeToCreate
        {
            get { return _typeToCreate; }
        }

        public CreatesAttribute(Type typeToCreate)
        {
            _typeToCreate = typeToCreate;
        }
    }
}
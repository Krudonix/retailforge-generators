using System;

namespace RetailForge.Generators.Abstractions
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class PropertyResolverTargetAttribute : Attribute
    {
        public Type TargetType { get; }

        public PropertyResolverTargetAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }
}

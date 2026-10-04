using System;

namespace RetailForge.Generators.Abstractions
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GenerateSQLAttribute : Attribute
    {
        public string TableName { get; }

        public GenerateSQLAttribute(string tableName = null)
        {
            TableName = tableName;
        }
    }
}

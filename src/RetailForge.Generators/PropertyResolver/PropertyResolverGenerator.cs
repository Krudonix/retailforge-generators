using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace RetailForge.Generators;

[Generator]
public class PropertyResolverGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "RetailForge.Generators.Abstractions.PropertyResolverTargetAttribute",
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, _) => ExtractModel(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(targets, static (ctx, model) => Emit(ctx, model!));
    }

    private static TypeModel? ExtractModel(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.Attributes[0].ConstructorArguments[0].Value is not INamedTypeSymbol type)
        {
            return null;
        }

        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var props = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.GetMethod != null)
            .Select(p => new PropertyModel(
                p.Name,
                p.Type.SpecialType == SpecialType.System_String,
                p.NullableAnnotation == NullableAnnotation.Annotated))
            .ToImmutableArray();

        return new TypeModel(ns, type.Name, props);
    }

    private static void Emit(SourceProductionContext context, TypeModel type)
    {
        var extensionClass = $"{type.ClassName}PropertyResolverExtensions";
        var sb = new StringBuilder();

        sb.AppendLine("using System;");

        if (type.Namespace is not null)
        {
            sb.AppendLine($"namespace {type.Namespace}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"    public static class {extensionClass}");
        sb.AppendLine("    {");

        EmitGetPropertyValue(sb, type);
        sb.AppendLine();
        EmitContainsProperty(sb, type);

        sb.AppendLine("    }");

        if (type.Namespace is not null)
            sb.AppendLine("}");

        var fileName = $"{(type.Namespace is null ? "" : type.Namespace.Replace('.', '_') + "_")}{type.ClassName}_PropertyResolver.g.cs";
        context.AddSource(fileName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitGetPropertyValue(StringBuilder sb, TypeModel type)
    {
        sb.AppendLine($"        public static string GetPropertyValue(this {type.ClassName} source, string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));");
        sb.AppendLine("            switch (propertyName.ToLowerInvariant())");
        sb.AppendLine("            {");

        foreach (var prop in type.Properties)
        {
            EmitValueCase(sb, prop.Name.ToLowerInvariant(), prop);
            if (prop.Name.Contains("_"))
                EmitValueCase(sb, prop.Name.Replace("_", "").ToLowerInvariant(), prop);
        }

        sb.AppendLine("                default: throw new NotImplementedException($\"Property '{propertyName}' is not supported.\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void EmitContainsProperty(StringBuilder sb, TypeModel type)
    {
        sb.AppendLine($"        public static bool ContainsProperty(this {type.ClassName} source, string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (propertyName == null) return false;");
        sb.AppendLine("            switch (propertyName.ToLowerInvariant())");
        sb.AppendLine("            {");

        foreach (var prop in type.Properties)
        {
            sb.AppendLine($"                case \"{prop.Name.ToLowerInvariant()}\": return true;");
            if (prop.Name.Contains("_"))
                sb.AppendLine($"                case \"{prop.Name.Replace("_", "").ToLowerInvariant()}\": return true;");
        }

        sb.AppendLine("                default: return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void EmitValueCase(StringBuilder sb, string key, PropertyModel prop)
    {
        if (prop.IsString)
        {
            sb.AppendLine($"                case \"{key}\": return source.{prop.Name};");
        }
        else if (prop.IsNullable)
        {
            sb.AppendLine($"                case \"{key}\": return source.{prop.Name}?.ToString();");
        }
        else
        {
            sb.AppendLine($"                case \"{key}\": return source.{prop.Name}.ToString();");
        }
    }
}

internal sealed class PropertyModel
{
    internal PropertyModel(string name, bool isString, bool isNullable)
    {
        Name = name;
        IsString = isString;
        IsNullable = isNullable;
    }

    internal string Name { get; }
    internal bool IsString { get; }
    internal bool IsNullable { get; }
}

internal sealed class TypeModel
{
    internal TypeModel(string? ns, string className, ImmutableArray<PropertyModel> properties)
    {
        Namespace = ns;
        ClassName = className;
        Properties = properties;
    }

    internal string? Namespace { get; }
    internal string ClassName { get; }
    internal ImmutableArray<PropertyModel> Properties { get; }
}

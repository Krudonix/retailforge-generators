using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RetailForge.Generators;

[Generator]
public class PocoGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "RetailForge.Generators.Abstractions.GeneratePocoAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is VariableDeclaratorSyntax,
                transform: static (ctx, _) => ExtractModel(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(targets, static (ctx, model) => Emit(ctx, model!));
    }

    private static PocoModel? ExtractModel(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IFieldSymbol field)
            return null;

        if (field.ConstantValue is not string sql)
            return null;

        var containingType = field.ContainingType;

        var ns = containingType.ContainingNamespace.IsGlobalNamespace
            ? null
            : containingType.ContainingNamespace.ToDisplayString();

        var properties = ParseColumns(sql);
        if (properties.IsEmpty)
            return null;

        return new PocoModel(ns, containingType.Name, properties);
    }

    private static ImmutableArray<string> ParseColumns(string sql)
    {
        // Extract the column list between SELECT and FROM
        var match = Regex.Match(sql, @"SELECT\s+(.*?)\s+FROM\b", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return ImmutableArray<string>.Empty;

        var columnSection = match.Groups[1].Value;

        // Strip -- comments
        columnSection = Regex.Replace(columnSection, @"--[^\n]*", "");

        // Strip /* */ comments (e.g. /**where**/)
        columnSection = Regex.Replace(columnSection, @"/\*.*?\*/", "", RegexOptions.Singleline);

        var props = new List<string>();

        foreach (var raw in columnSection.Split(','))
        {
            var col = raw.Trim();
            if (string.IsNullOrWhiteSpace(col))
                continue;

            // Handle AS alias: take everything after AS
            var asMatch = Regex.Match(col, @"\bAS\s+(\w+)\s*$", RegexOptions.IgnoreCase);
            string name;
            if (asMatch.Success)
            {
                name = asMatch.Groups[1].Value;
            }
            else
            {
                // Strip table prefix (e.g. i.sid -> sid)
                var dotIdx = col.LastIndexOf('.');
                name = dotIdx >= 0 ? col.Substring(dotIdx + 1).Trim() : col.Trim();
            }

            var pascal = ToPascalCase(name);
            if (!string.IsNullOrWhiteSpace(pascal))
                props.Add(pascal);
        }

        return props.ToImmutableArray();
    }

    private static string ToPascalCase(string name)
    {
        var parts = name.Split('_');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part.Substring(1).ToLowerInvariant());
        }
        return sb.ToString();
    }

    private static void Emit(SourceProductionContext context, PocoModel model)
    {
        var sb = new StringBuilder();
        var extensionClass = $"{model.ClassName}ResultPropertyResolverExtensions";
        var resultType = $"{model.ClassName}.Result";

        sb.AppendLine("using System;");
        sb.AppendLine();

        if (model.Namespace is not null)
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        var i = model.Namespace is not null ? "    " : "";

        // Result nested class
        sb.AppendLine($"{i}public partial class {model.ClassName}");
        sb.AppendLine($"{i}{{");
        sb.AppendLine($"{i}    public class Result");
        sb.AppendLine($"{i}    {{");
        foreach (var prop in model.Properties)
            sb.AppendLine($"{i}        public string {prop} {{ get; set; }}");
        sb.AppendLine($"{i}    }}");
        sb.AppendLine($"{i}}}");
        sb.AppendLine();

        // Extension methods
        sb.AppendLine($"{i}public static class {extensionClass}");
        sb.AppendLine($"{i}{{");

        sb.AppendLine($"{i}    public static string GetPropertyValue(this {resultType} source, string propertyName)");
        sb.AppendLine($"{i}    {{");
        sb.AppendLine($"{i}        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));");
        sb.AppendLine($"{i}        switch (propertyName.ToLowerInvariant())");
        sb.AppendLine($"{i}        {{");
        foreach (var prop in model.Properties)
            sb.AppendLine($"{i}            case \"{prop.ToLowerInvariant()}\": return source.{prop};");
        sb.AppendLine($"{i}            default: throw new NotImplementedException($\"Property '{{propertyName}}' is not supported.\");");
        sb.AppendLine($"{i}        }}");
        sb.AppendLine($"{i}    }}");
        sb.AppendLine();

        sb.AppendLine($"{i}    public static bool ContainsProperty(this {resultType} source, string propertyName)");
        sb.AppendLine($"{i}    {{");
        sb.AppendLine($"{i}        if (propertyName == null) return false;");
        sb.AppendLine($"{i}        switch (propertyName.ToLowerInvariant())");
        sb.AppendLine($"{i}        {{");
        foreach (var prop in model.Properties)
            sb.AppendLine($"{i}            case \"{prop.ToLowerInvariant()}\": return true;");
        sb.AppendLine($"{i}            default: return false;");
        sb.AppendLine($"{i}        }}");
        sb.AppendLine($"{i}    }}");

        sb.AppendLine($"{i}}}");

        if (model.Namespace is not null)
            sb.AppendLine("}");

        var fileName = $"{(model.Namespace is null ? "" : model.Namespace.Replace('.', '_') + "_")}{model.ClassName}_Poco.g.cs";
        context.AddSource(fileName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}

internal sealed class PocoModel
{
    internal PocoModel(string? ns, string className, ImmutableArray<string> properties)
    {
        Namespace = ns;
        ClassName = className;
        Properties = properties;
    }

    internal string? Namespace { get; }
    internal string ClassName { get; }
    internal ImmutableArray<string> Properties { get; }
}

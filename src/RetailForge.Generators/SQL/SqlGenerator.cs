using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace RetailForge.Generators;

[Generator]
public class SqlGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "RetailForge.Generators.Abstractions.GenerateSQLAttribute";
    private const string KeyAttributeFullName = "System.ComponentModel.DataAnnotations.KeyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ExtractModel(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(targets, static (ctx, model) => Emit(ctx, model!));
    }

    private static SqlModel? ExtractModel(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
            return null;

        var attr = ctx.Attributes[0];
        var tableName = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : type.Name;

        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var allProps = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        !p.IsStatic &&
                        p.GetMethod != null &&
                        p.SetMethod != null)
            .ToList();

        // Detect PK: [Key] attribute, then Id or {ClassName}Id convention
        var keyProp = allProps.FirstOrDefault(p =>
            p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == KeyAttributeFullName));

        if (keyProp is null)
            keyProp = allProps.FirstOrDefault(p =>
                p.Name == "Id" || p.Name == $"{type.Name}Id");

        var props = allProps
            .Select(p => new SqlPropertyModel(p.Name, p.Equals(keyProp, SymbolEqualityComparer.Default)))
            .ToImmutableArray();

        return new SqlModel(ns, type.Name, tableName, props);
    }

    private static void Emit(SourceProductionContext context, SqlModel model)
    {
        var nonKeyProps = model.Properties.Where(p => !p.IsKey).ToList();
        var keyProp = model.Properties.FirstOrDefault(p => p.IsKey);

        var sb = new StringBuilder();

        if (model.Namespace is not null)
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"    public static class {model.ClassName}Queries");
        sb.AppendLine("    {");

        // Insert
        sb.AppendLine("        public static class Insert");
        sb.AppendLine("        {");
        sb.AppendLine($"            public const string Sql =");
        EmitSqlString(sb, BuildInsert(model.TableName, nonKeyProps.Select(p => p.Name).ToList()));
        sb.AppendLine("        }");
        sb.AppendLine();

        // Update
        if (keyProp is not null)
        {
            sb.AppendLine("        public static class Update");
            sb.AppendLine("        {");
            sb.AppendLine($"            public const string Sql =");
            EmitSqlString(sb, BuildUpdate(model.TableName, nonKeyProps.Select(p => p.Name).ToList(), keyProp.Name));
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine("        public static class Delete");
            sb.AppendLine("        {");
            sb.AppendLine($"            public const string Sql =");
            EmitSqlString(sb, BuildDelete(model.TableName, keyProp.Name));
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Select
        sb.AppendLine("        public static class Select");
        sb.AppendLine("        {");
        sb.AppendLine($"            public const string All =");
        EmitSqlString(sb, BuildSelectAll(model.TableName, model.Properties.Select(p => p.Name).ToList()));
        if (keyProp is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"            public const string ById =");
            EmitSqlString(sb, BuildSelectById(model.TableName, model.Properties.Select(p => p.Name).ToList(), keyProp.Name));
        }
        sb.AppendLine("        }");

        sb.AppendLine("    }");

        if (model.Namespace is not null)
            sb.AppendLine("}");

        var fileName = $"{(model.Namespace is null ? "" : model.Namespace.Replace('.', '_') + "_")}{model.ClassName}Queries.g.cs";
        context.AddSource(fileName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // Emit a SQL string as concatenated lines for readability in the generated file
    private static void EmitSqlString(StringBuilder sb, string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            bool last = i == lines.Length - 1;
            var separator = last ? "\";" : "\\n\" +";
            sb.AppendLine($"                \"{lines[i]}{separator}");
        }
    }

    private static string[] BuildInsert(string table, System.Collections.Generic.List<string> cols)
    {
        var colList  = string.Join(", ", cols);
        var paramList = string.Join(", ", cols.Select(c => $"@{c}"));
        return new[]
        {
            $"INSERT INTO {table}",
            $"    ({colList})",
            $"VALUES",
            $"    ({paramList})",
        };
    }

    private static string[] BuildUpdate(string table, System.Collections.Generic.List<string> cols, string key)
    {
        var setClauses = cols.Select(c => $"    {c} = @{c}").ToArray();
        var lines = new System.Collections.Generic.List<string>();
        lines.Add($"UPDATE {table}");
        lines.Add("SET");
        for (int i = 0; i < setClauses.Length; i++)
            lines.Add(setClauses[i] + (i < setClauses.Length - 1 ? "," : ""));
        lines.Add("WHERE");
        lines.Add($"    {key} = @{key}");
        return lines.ToArray();
    }

    private static string[] BuildDelete(string table, string key)
    {
        return new[]
        {
            $"DELETE FROM {table}",
            "WHERE",
            $"    {key} = @{key}",
        };
    }

    private static string[] BuildSelectAll(string table, System.Collections.Generic.List<string> cols)
    {
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("SELECT");
        for (int i = 0; i < cols.Count; i++)
            lines.Add($"    {cols[i]}" + (i < cols.Count - 1 ? "," : ""));
        lines.Add($"FROM {table}");
        return lines.ToArray();
    }

    private static string[] BuildSelectById(string table, System.Collections.Generic.List<string> cols, string key)
    {
        var lines = BuildSelectAll(table, cols).ToList();
        lines.Add("WHERE");
        lines.Add($"    {key} = @{key}");
        return lines.ToArray();
    }
}

internal sealed class SqlPropertyModel
{
    internal SqlPropertyModel(string name, bool isKey)
    {
        Name = name;
        IsKey = isKey;
    }

    internal string Name { get; }
    internal bool IsKey { get; }
}

internal sealed class SqlModel
{
    internal SqlModel(string? ns, string className, string tableName, ImmutableArray<SqlPropertyModel> properties)
    {
        Namespace = ns;
        ClassName = className;
        TableName = tableName;
        Properties = properties;
    }

    internal string? Namespace { get; }
    internal string ClassName { get; }
    internal string TableName { get; }
    internal ImmutableArray<SqlPropertyModel> Properties { get; }
}

using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
namespace RetailForge.Generators
{
    //[Generator]
    public sealed class GraphQLModelGenerator : IIncrementalGenerator
    {
        private const string AttributeFullName = "RetailForge.Generators.Abstractions.GraphQLResponseModelAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methods = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) =>
                    {
                        if (node is not MethodDeclarationSyntax method)
                        {
                            return false;
                        }

                        return method.AttributeLists
                            .SelectMany(al => al.Attributes)
                            .Any(a => a.Name.ToString().Contains("GraphQLResponseModel"));
                    },
                    transform: static (ctx, _) => (MethodDeclarationSyntax)ctx.Node)
                .Collect();

            context.RegisterSourceOutput(
                methods.Combine(context.CompilationProvider),
                static (ctx, pair) =>
                {
                    var attributeSymbol = pair.Right.GetTypeByMetadataName(AttributeFullName);
                    if (attributeSymbol is null)
                    {
                        return;
                    }

                    foreach (var method in pair.Left)
                    {
                        var generated = GenerateModelClass(method, pair.Right);
                        if (string.IsNullOrEmpty(generated.code))
                        {
                            continue;
                        }

                        var methodSymbol = pair.Right
                            .GetSemanticModel(method.SyntaxTree)
                            .GetDeclaredSymbol(method);

                        var hint = methodSymbol != null
                            ? $"{methodSymbol.ContainingNamespace.ToDisplayString().Replace(".", "_")}_{methodSymbol.ContainingType.Name}_{methodSymbol.Name}_GraphQLModel.g.cs"
                            : $"{generated.className}_{Guid.NewGuid():N}_GraphQLModel.g.cs";

                        ctx.AddSource(hint, SourceText.From(generated.code, Encoding.UTF8));
                    }
                });
        }

        private static (string className, string code) GenerateModelClass(MethodDeclarationSyntax method, Compilation compilation)
        {
            var query = method.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression) || l.IsKind(SyntaxKind.InterpolatedStringExpression))
                ?.ToString();

            if (string.IsNullOrWhiteSpace(query))
                return ("", "");

            var ns = method.Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()
                ?.Name
                .ToString();


            // For now, just simulate based on the known structure (later: real GraphQL parser)
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    public class ProductVariants");
            sb.AppendLine("    {");
            sb.AppendLine("            public string Id { get; set; }");
            sb.AppendLine("            public string Barcode { get; set; }");
            sb.AppendLine("            public string Sku { get; set; }");
            sb.AppendLine("            public int InventoryQuantity { get; set; }");
            sb.AppendLine("            public Product Product { get; set; }");
            sb.AppendLine("    }");
            sb.AppendLine("}");


            return ("ProductVariants", sb.ToString());
        }
    }

}

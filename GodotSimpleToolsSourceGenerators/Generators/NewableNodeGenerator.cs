using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace GodotSimpleTools.Generators;

[Generator]
public class NewableNodeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 收集所有带有 NewableNodeAttribute 的类
        var newableNodeClassesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => 
                    syntaxNode is ClassDeclarationSyntax,
                transform: static (generatorSyntaxContext, cancellationToken) =>
                {
                    var classDeclaration = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
                    var semanticModel = generatorSyntaxContext.SemanticModel;
                    
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);
                    if (classSymbol == null)
                        return default;

                    // 检查是否带有 NewableNodeAttribute
                    var newableNodeAttribute = classSymbol.GetAttributes()
                        .FirstOrDefault(ad =>
                            ad.AttributeClass != null && 
                            SymbolEqualityComparer.Default.Equals(
                                ad.AttributeClass, 
                                generatorSyntaxContext.SemanticModel.Compilation
                                    .GetTypeByMetadataName("GodotSimpleTools.NewableNodeAttribute")) ||
                            ad.AttributeClass?.Name == "NewableNodeAttribute");
                    
                    if (newableNodeAttribute == null)
                        return default;
                    
                    // 检查是否继承自 Node
                    bool isNodeClass = IsOrInheritsFromNode(classSymbol);
                    if (!isNodeClass)
                        return default;
                    
                    // 解析场景路径参数
                    string? scenePath = null;
                    
                    if (newableNodeAttribute.ConstructorArguments.Length > 0)
                    {
                        var firstArg = newableNodeAttribute.ConstructorArguments[0];
                        if (firstArg is { Kind: TypedConstantKind.Primitive, Type.SpecialType: SpecialType.System_String })
                        {
                            scenePath = firstArg.Value?.ToString();
                        }
                    }
                    
                    // 检查命名参数
                    foreach (var arg in newableNodeAttribute.NamedArguments)
                    {
                        if (arg.Key == "Path")
                        {
                            scenePath = arg.Value.Value?.ToString();
                        }
                    }
                    
                    if (string.IsNullOrEmpty(scenePath))
                        return default;
                    
                    return (ClassSymbol: classSymbol, 
                           ClassDeclaration: classDeclaration, 
                           ScenePath: scenePath);
                })
            .Where(static x => x.ClassSymbol != null)
            .Collect();

        // 2. 为每个类生成代码
        var groupedByClassProvider = newableNodeClassesProvider
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) =>
            {
                var (newableNodeClasses, _) = pair;
                var result = new List<(INamedTypeSymbol, ClassDeclarationSyntax, string)>();
                
                foreach (var (classSymbol, classDeclaration, scenePath) in newableNodeClasses)
                {
                    if (classSymbol == null) continue;
                    result.Add((classSymbol, classDeclaration, scenePath!));
                }
                
                return result;
            });

        // 3. 注册代码生成
        context.RegisterSourceOutput(groupedByClassProvider,
            static (sourceProductionContext, newableNodeClasses) =>
            {
                foreach (var (classSymbol, _, scenePath) in newableNodeClasses)
                {
                    try
                    {
                        var sourceCode = GenerateNewableNodeClass(classSymbol, scenePath);
                        var hintName = $"{classSymbol.ToDisplayString().Replace(".", "_")}.NewableNode.GST.g.cs";
        
                        sourceProductionContext.AddSource(hintName,
                            SourceText.From(sourceCode, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "GSTNN001",
                                "NewableNode代码生成错误",
                                $"生成{classSymbol.Name}的NewableNode代码时出错: {ex.Message}",
                                "CodeGeneration",
                                DiagnosticSeverity.Warning,
                                true),
                            Location.None));
                    }
                }
            });
    }

    private static string GenerateNewableNodeClass(INamedTypeSymbol classSymbol, string scenePath)
    {
        var sb = new StringBuilder();
        var className = classSymbol.Name;

        // 获取基类信息
        var baseType = classSymbol.BaseType;
        var baseTypeName = baseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::Godot.Node";

        // sb.AppendLine($"using Godot;");
        // sb.AppendLine();
        // 命名空间
        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine($"namespace {classSymbol.ContainingNamespace.ToDisplayString()};");
            sb.AppendLine();
        }

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("// 由GodotSimpleTools的NewableNode生成器生成");

        // 生成部分类声明，包含基类继承
        sb.Append($"public partial class {className}");

        // 添加基类继承
        if (baseType != null && baseType.Name != "object")
        {
            // 获取基类的完全限定名
            var fullBaseTypeName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append($" : {fullBaseTypeName}");
        }

        sb.AppendLine();
        sb.AppendLine("{");

        // 生成New方法
        sb.AppendLine($"    public static {className} New(bool duplicate = true)");
        sb.AppendLine("    {");
        sb.AppendLine(
            $"        var node = global::Godot.ResourceLoader.Load<global::Godot.PackedScene>(\"{EscapeString(scenePath)}\").Instantiate();");
        sb.AppendLine($"        if (duplicate) node = node.Duplicate();");
        sb.AppendLine($"        return node as {className};");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeString(string input)
    {
        // 转义字符串中的特殊字符
        return input
            .Replace("\\", @"\\")
            .Replace("\"", "\\\"")
            .Replace("\n", @"\n")
            .Replace("\r", @"\r")
            .Replace("\t", @"\t");
    }
    
    private static bool IsOrInheritsFromNode(INamedTypeSymbol? typeSymbol)
    {
        while (typeSymbol != null)
        {
            if (typeSymbol.Name == "Node" && 
                typeSymbol.ContainingNamespace?.ToDisplayString() == "Godot")
            {
                return true;
            }
            
            typeSymbol = typeSymbol.BaseType;
        }
        
        return false;
    }
}
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
public class NodePathGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 收集所有带有NodePathAttribute的符号
        var nodePathSymbolsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => 
                    syntaxNode is FieldDeclarationSyntax or PropertyDeclarationSyntax,
                transform: static (generatorSyntaxContext, cancellationToken) =>
                {
                    var node = generatorSyntaxContext.Node;
                    var semanticModel = generatorSyntaxContext.SemanticModel;
                    
                    switch (node)
                    {
                        case FieldDeclarationSyntax fieldSyntax:
                        {
                            var nodePathFields = new List<(IFieldSymbol, string?)>();
                            
                            foreach (var variable in fieldSyntax.Declaration.Variables)
                            {
                                if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is IFieldSymbol fieldSymbol)
                                {
                                    var attributes = fieldSymbol.GetAttributes();
                                    var nodePathAttribute = attributes.FirstOrDefault(ad =>
                                        ad.AttributeClass != null && 
                                        SymbolEqualityComparer.Default.Equals(
                                            ad.AttributeClass, 
                                            generatorSyntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("GodotSimpleTools.NodePathAttribute")) ||
                                        ad.AttributeClass?.Name == "NodePathAttribute");
                                    
                                    if (nodePathAttribute != null)
                                    {
                                        // 解析路径参数
                                        string? pathValue = null;
                                        
                                        if (nodePathAttribute.ConstructorArguments.Length > 0)
                                        {
                                            var firstArg = nodePathAttribute.ConstructorArguments[0];
                                            if (firstArg is { Kind: TypedConstantKind.Primitive, Type.SpecialType: SpecialType.System_String })
                                            {
                                                pathValue = firstArg.Value?.ToString();
                                            }
                                        }
                                        
                                        // 检查命名参数
                                        foreach (var arg in nodePathAttribute.NamedArguments)
                                        {
                                            if (arg is { Key: "Value", Value.Value: not null })
                                            {
                                                pathValue = arg.Value.Value.ToString();
                                            }
                                        }
                                        
                                        nodePathFields.Add((fieldSymbol, pathValue));
                                    }
                                }
                            }

                            if (nodePathFields.Any())
                            {
                                return (ClassSyntax: node.Parent as ClassDeclarationSyntax, 
                                        NodePathFields: nodePathFields,
                                        NodePathProperties: new List<(IPropertySymbol, string?)>());
                            }
                            break;
                        }
                        case PropertyDeclarationSyntax propertySyntax:
                        {
                            if (semanticModel.GetDeclaredSymbol(propertySyntax, cancellationToken) is { } propertySymbol)
                            {
                                var attributes = propertySymbol.GetAttributes();
                                var nodePathAttribute = attributes.FirstOrDefault(ad =>
                                    ad.AttributeClass != null && 
                                    SymbolEqualityComparer.Default.Equals(
                                        ad.AttributeClass, 
                                        generatorSyntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("GodotSimpleTools.NodePathAttribute")) ||
                                    ad.AttributeClass?.Name == "NodePathAttribute");
                                
                                if (nodePathAttribute != null)
                                {
                                    // 解析路径参数
                                    string? pathValue = null;
                                    
                                    if (nodePathAttribute.ConstructorArguments.Length > 0)
                                    {
                                        var firstArg = nodePathAttribute.ConstructorArguments[0];
                                        if (firstArg is { Kind: TypedConstantKind.Primitive, Type.SpecialType: SpecialType.System_String })
                                        {
                                            pathValue = firstArg.Value?.ToString();
                                        }
                                    }
                                    
                                    // 检查命名参数
                                    foreach (var arg in nodePathAttribute.NamedArguments)
                                    {
                                        if (arg is { Key: "Value", Value.Value: not null })
                                        {
                                            pathValue = arg.Value.Value.ToString();
                                        }
                                    }
                                    
                                    var nodePathProperties = new List<(IPropertySymbol, string?)>
                                    {
                                        (propertySymbol, pathValue)
                                    };
                                
                                    return (ClassSyntax: node.Parent as ClassDeclarationSyntax,
                                            NodePathFields: new List<(IFieldSymbol, string?)>(),
                                            NodePathProperties: nodePathProperties);
                                }
                            }
                            break;
                        }
                    }
                    
                    return (ClassSyntax: null, 
                            NodePathFields: new List<(IFieldSymbol, string?)>(),
                            NodePathProperties: new List<(IPropertySymbol, string?)>());
                })
            .Where(static x => x.ClassSyntax != null)
            .Collect();

        // 2. 按类分组
        var groupedByClassProvider = nodePathSymbolsProvider
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) =>
            {
                var (symbols, compilation) = pair;
                var grouped = new Dictionary<INamedTypeSymbol, 
                    (List<(IFieldSymbol, string?)>, List<(IPropertySymbol, string?)>)>(SymbolEqualityComparer.Default);
                
                foreach (var (classSyntax, nodePathFields, nodePathProperties) in symbols)
                {
                    if (classSyntax == null) continue;
                    
                    var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

                    if (semanticModel.GetDeclaredSymbol(classSyntax) is not { } classSymbol) continue;
                    
                    if (!grouped.TryGetValue(classSymbol, out var members))
                    {
                        members = (new List<(IFieldSymbol, string?)>(), 
                                 new List<(IPropertySymbol, string?)>());
                        grouped[classSymbol] = members;
                    }
                    
                    members.Item1.AddRange(nodePathFields);
                    members.Item2.AddRange(nodePathProperties);
                }
                
                return grouped;
            });

        // 3. 为每个类生成代码
        context.RegisterSourceOutput(groupedByClassProvider,
            static (sourceProductionContext, classGroups) =>
            {
                foreach (var kvp in classGroups)
                {
                    var classSymbol = kvp.Key;
                    var nodePathFields = kvp.Value.Item1;
                    var nodePathProperties = kvp.Value.Item2;
                    
                    if (!nodePathFields.Any() && !nodePathProperties.Any())
                        continue;
                            
                    try
                    {
                        var sourceCode = GeneratePartialClass(classSymbol,
                            nodePathFields,
                            nodePathProperties);
                        var hintName = $"{classSymbol.ToDisplayString().Replace(".", "_")}.NodePath.GST.g.cs";
        
                        sourceProductionContext.AddSource(hintName,
                            SourceText.From(sourceCode, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        // 记录错误，但不中断编译
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "GSTNP001",
                                "NodePath代码生成错误",
                                $"生成{classSymbol.Name}的NodePath代码时出错: {ex.Message}",
                                "CodeGeneration",
                                DiagnosticSeverity.Warning,
                                true),
                            Location.None));
                    }
                }
            });
    }

    private static string GeneratePartialClass(
    INamedTypeSymbol classSymbol,
    List<(IFieldSymbol Field, string? Path)> nodePathFields,
    List<(IPropertySymbol Property, string? Path)> nodePathProperties)
{
    var sb = new StringBuilder();

    // 命名空间
    if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
    {
        sb.AppendLine($"namespace {classSymbol.ContainingNamespace.ToDisplayString()};");
        sb.AppendLine();
    }

    sb.AppendLine("// <auto-generated />");
    sb.AppendLine("#nullable enable");
    sb.AppendLine();
    sb.AppendLine("// 由GodotSimpleTools的NodePath生成器生成");

    bool isNodeClass = IsOrInheritsFromNode(classSymbol);

    sb.AppendLine($"public partial class {classSymbol.Name}");
    sb.AppendLine("{");

    if (isNodeClass && (nodePathFields.Any() || nodePathProperties.Any()))
    {
        // 生成独立的生命周期方法
        sb.AppendLine("    private void _GstInitializeNodePath()");
        sb.AppendLine("    {");

        // 为每个字段生成初始化代码
        foreach (var (field, path) in nodePathFields)
        {
            if (string.IsNullOrEmpty(path))
            {
                var fieldTypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine(
                    $"        {field.Name} ??= GetChildren().ToList().FirstOrDefault(n => n is {fieldTypeName}) as {fieldTypeName};");
            }
            else
            {
                var fieldTypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (path != null)
                    sb.AppendLine($"        {field.Name} ??= GetNode<{fieldTypeName}>(\"{EscapeString(path)}\");");
            }
        }

        // 为每个属性生成初始化代码
        foreach (var (property, path) in nodePathProperties)
        {
            if (string.IsNullOrEmpty(path))
            {
                var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine(
                    $"        {property.Name} ??= GetChildren().ToList().FirstOrDefault(n => n is {propertyTypeName}) as {propertyTypeName};");
            }
            else
            {
                var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (path != null)
                    sb.AppendLine(
                        $"        {property.Name} ??= GetNode<{propertyTypeName}>(\"{EscapeString(path)}\");");
            }
        }

        sb.AppendLine("    }");
    }

    sb.AppendLine("}");

    return sb.ToString();
}
    
    private static string EscapeString(string input)
    {
        // 简单的字符串转义
        return input.Replace("\\", @"\\").Replace("\"", "\\\"");
    }

    private static bool IsOrInheritsFromNode(INamedTypeSymbol typeSymbol)
    {
        while (true)
        {
            // 检查是否直接继承自Node
            if (typeSymbol.BaseType == null) return false;
            var baseType = typeSymbol.BaseType;

            // 检查是否是Node类型
            if (baseType.Name == "Node" && baseType.ContainingNamespace?.ToDisplayString() == "Godot")
            {
                return true;
            }

            // 递归检查基类
            typeSymbol = baseType;
        }
    }
}
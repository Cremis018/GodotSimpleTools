using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSimpleToolsSourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReceiverAnalyzer : DiagnosticAnalyzer
{
    private const string ReceiverAttributeName = "GodotSimpleTools.Attributes.ReceiverAttribute";
    private const string DiagnosticId = "GST001";
    private const string Title = "Receiver attribute usage issue";
    private const string MessageFormat = "{0}";
    private const string Description = "Checks for proper usage of Receiver attributes.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor NotPartialRule = new(
        "GST002",
        "Type must be partial",
        "Type '{0}' using Receiver attribute must be partial",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types using Receiver attribute must be declared as partial."
    );

    private static DiagnosticDescriptor InvalidEventExpressionRule = new(
        "GST003",
        "Invalid event expression",
        "Event expression should be nameof() or string literal",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Event expression in Receiver attribute should be nameof() or string literal."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
        ImmutableArray.Create(NotPartialRule, InvalidEventExpressionRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        
        // 检查类型是否是partial
        CheckIfPartial(context, namedType);
        
        // 检查Receiver特性的使用
        CheckReceiverAttributes(context, namedType);
    }

    private static void CheckIfPartial(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        bool isPartial = false;
        foreach (var syntaxRef in namedType.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl)
            {
                isPartial = typeDecl.Modifiers.Any(m => m.Text == "partial");
                if (isPartial) break;
            }
        }

        // 如果类型不是partial但使用了Receiver特性，则报告错误
        if (!isPartial)
        {
            foreach (var member in namedType.GetMembers())
            {
                if (member is IMethodSymbol method)
                {
                    foreach (var attribute in method.GetAttributes())
                    {
                        if (attribute.AttributeClass?.ToDisplayString() == ReceiverAttributeName)
                        {
                            foreach (var syntaxRef in namedType.DeclaringSyntaxReferences)
                            {
                                var diagnostic = Diagnostic.Create(NotPartialRule, syntaxRef.GetSyntax().GetLocation(), namedType.Name);
                                context.ReportDiagnostic(diagnostic);
                            }
                            return;
                        }
                    }
                }
            }
        }
    }

    private static void CheckReceiverAttributes(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        foreach (var member in namedType.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                foreach (var attribute in method.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() == ReceiverAttributeName)
                    {
                        // 检查第一个参数（事件表达式）
                        if (attribute.ConstructorArguments.Length > 0)
                        {
                            var firstArg = attribute.ConstructorArguments[0];
                            var rawExpression = firstArg.ToCSharpString();
                            
                            // 检查是否是有效的表达式（nameof() 或字符串字面量）
                            if (rawExpression.StartsWith("nameof(") && rawExpression.EndsWith(")") ||
                                rawExpression.StartsWith("\"") && rawExpression.EndsWith("\"")) continue;
                            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
                            {
                                if (syntaxRef.GetSyntax() is MethodDeclarationSyntax methodDecl)
                                {
                                    // 查找对应的特性语法
                                    var attributeSyntax = methodDecl.AttributeLists
                                        .SelectMany(al => al.Attributes)
                                        .FirstOrDefault(a =>
                                        {
                                            var symbol = ModelExtensions.GetSymbolInfo(context.Compilation.GetSemanticModel(methodDecl.SyntaxTree), a).Symbol;
                                            return symbol is IMethodSymbol m && 
                                                   m.ContainingType?.ToDisplayString() == ReceiverAttributeName;
                                        });

                                    if (attributeSyntax != null)
                                    {
                                        var diagnostic = Diagnostic.Create(InvalidEventExpressionRule, 
                                            attributeSyntax.GetLocation());
                                        context.ReportDiagnostic(diagnostic);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
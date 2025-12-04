using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSimpleToolsSourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NotifyAnalyzer : DiagnosticAnalyzer
{
    private const string NotifyAttributeName = "GodotSimpleTools.NotifyAttribute";
    private const string DiagnosticId = "GST004";
    private const string Title = "Notify attribute usage issue";
    private const string MessageFormat = "{0}";
    private const string Description = "Checks for proper usage of Notify attributes.";
    private const string Category = "Usage";

    private static DiagnosticDescriptor NotPartialRule = new(
        "GST005",
        "Type must be partial",
        "Type '{0}' using Notify attribute must be partial",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types using Notify attribute must be declared as partial."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
        ImmutableArray.Create(NotPartialRule);

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

        // 如果类型不是partial但使用了Notify特性，则报告错误
        if (!isPartial)
        {
            foreach (var member in namedType.GetMembers())
            {
                if (member is IPropertySymbol property)
                {
                    foreach (var attribute in property.GetAttributes())
                    {
                        if (attribute.AttributeClass?.ToDisplayString() == NotifyAttributeName)
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
}
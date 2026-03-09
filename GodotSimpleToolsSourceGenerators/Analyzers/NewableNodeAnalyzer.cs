using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace GodotSimpleTools.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NewableNodeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GST009";
    
    private const string Category = "Usage";
    
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, 
        "NewableNodeAttribute 使用错误", 
        "{0}", 
        Category, 
        DiagnosticSeverity.Error, 
        isEnabledByDefault: true, 
        description: "NewableNodeAttribute 使用规则检查.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        // 检查类是否有 NewableNodeAttribute
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken);
        if (classSymbol == null) return;
        
        var newableNodeAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(ad => 
                ad.AttributeClass?.Name == "NewableNodeAttribute" ||
                (ad.AttributeClass?.BaseType?.Name == "NewableNodeAttribute") ||
                SymbolEqualityComparer.Default.Equals(
                    ad.AttributeClass, 
                    context.Compilation.GetTypeByMetadataName("GodotSimpleTools.NewableNodeAttribute")));
        
        if (newableNodeAttribute == null) return;
        
        // 检查类是否继承自 Node
        bool isNodeClass = IsOrInheritsFromNode(classSymbol);
        if (!isNodeClass)
        {
            var diagnostic = Diagnostic.Create(
                Rule, 
                classDeclaration.Identifier.GetLocation(),
                "NewableNodeAttribute 只能用于继承自 Godot.Node 的类");
            context.ReportDiagnostic(diagnostic);
            return;
        }
        
        // 检查属性参数
        if (newableNodeAttribute.ConstructorArguments.Length == 0)
        {
            var diagnostic = Diagnostic.Create(
                Rule, 
                classDeclaration.Identifier.GetLocation(),
                "NewableNodeAttribute 需要提供场景路径参数");
            context.ReportDiagnostic(diagnostic);
            return;
        }
        
        var firstArg = newableNodeAttribute.ConstructorArguments[0];
        if (firstArg.Kind != TypedConstantKind.Primitive || 
            firstArg.Type?.SpecialType != SpecialType.System_String ||
            string.IsNullOrEmpty(firstArg.Value?.ToString()))
        {
            var diagnostic = Diagnostic.Create(
                Rule, 
                classDeclaration.Identifier.GetLocation(),
                "NewableNodeAttribute 的场景路径参数必须是有效的字符串");
            context.ReportDiagnostic(diagnostic);
        }
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
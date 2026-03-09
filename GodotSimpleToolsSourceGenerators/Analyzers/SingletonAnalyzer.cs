using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace GodotSimpleTools.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SingletonAnalyzer : DiagnosticAnalyzer
{
    public const string InvalidAutoLoadForNormalClassId = "GST006";
    public const string AutoLoadRecommendedForNodeId = "GST007";
    public const string ClassShouldBePartialId = "GST008";
    
    private static readonly string InvalidAutoLoadForNormalClassTitle = 
        "非Node类不支持AutoLoad单例模式";
    private static readonly string InvalidAutoLoadForNormalClassMessageFormat = 
        "非Node类'{0}'使用了AutoLoad单例模式，将自动按Lazy模式处理";
    
    private static readonly string AutoLoadRecommendedForNodeTitle = 
        "推荐Node类使用AutoLoad单例模式";
    private static readonly string AutoLoadRecommendedForNodeMessageFormat = 
        "Node类'{0}'使用{1}单例模式，建议使用AutoLoad模式以获得更好的性能";
    
    private static readonly string ClassShouldBePartialTitle = 
        "Singleton类应该是partial类";
    private static readonly string ClassShouldBePartialMessageFormat = 
        "使用了[Singleton]特性的类'{0}'应该是partial类";
    
    private const string Category = "Usage";
    
    private static readonly DiagnosticDescriptor InvalidAutoLoadForNormalClassRule = new DiagnosticDescriptor(
        InvalidAutoLoadForNormalClassId,
        InvalidAutoLoadForNormalClassTitle,
        InvalidAutoLoadForNormalClassMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "非Node类不支持AutoLoad模式，将自动转换为Lazy模式.");
    
    private static readonly DiagnosticDescriptor AutoLoadRecommendedForNodeRule = new DiagnosticDescriptor(
        AutoLoadRecommendedForNodeId,
        AutoLoadRecommendedForNodeTitle,
        AutoLoadRecommendedForNodeMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Node类推荐使用AutoLoad模式以获得更好的性能和自动管理.");
    
    private static readonly DiagnosticDescriptor ClassShouldBePartialRule = new DiagnosticDescriptor(
        ClassShouldBePartialId,
        ClassShouldBePartialTitle,
        ClassShouldBePartialMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "使用[Singleton]特性的类需要标记为partial以便代码生成器生成代码.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(
            InvalidAutoLoadForNormalClassRule, 
            AutoLoadRecommendedForNodeRule, 
            ClassShouldBePartialRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSymbolAction(AnalyzeClassSymbol, SymbolKind.NamedType);
    }

    private void AnalyzeClassSymbol(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        
        // 检查是否带有SingletonAttribute
        var singletonAttribute = namedTypeSymbol.GetAttributes()
            .FirstOrDefault(ad =>
                ad.AttributeClass != null && 
                (ad.AttributeClass.Name == "SingletonAttribute" || 
                 ad.AttributeClass.Name == "Singleton"));
        
        if (singletonAttribute == null)
            return;
        
        // 解析模式参数
        var mode = SingletonMode.Lazy;
        
        if (singletonAttribute.ConstructorArguments.Length > 0)
        {
            var firstArg = singletonAttribute.ConstructorArguments[0];
            if (firstArg is { Kind: TypedConstantKind.Enum, Type.Name: "SingletonMode" })
            {
                mode = (SingletonMode)(int)firstArg.Value!;
            }
        }
        
        // 检查命名参数
        foreach (var arg in singletonAttribute.NamedArguments.Where(arg => arg.Key == "Mode"))
        {
            mode = (SingletonMode)(int)arg.Value.Value!;
        }
        
        // 检查1: 类是否标记为partial
        var classDeclaration = GetClassDeclaration(namedTypeSymbol, context.CancellationToken);
        if (classDeclaration != null)
        {
            bool isPartialClass = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartialClass)
            {
                var diagnostic = Diagnostic.Create(
                    ClassShouldBePartialRule,
                    classDeclaration.Identifier.GetLocation(),
                    namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
        
        // 检查是否是Node派生类
        bool isNodeClass = IsOrInheritsFromNode(namedTypeSymbol);

        switch (isNodeClass)
        {
            // 检查2: 非Node类使用AutoLoad模式
            case false when mode == SingletonMode.AutoLoad:
            {
                if (classDeclaration != null)
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidAutoLoadForNormalClassRule,
                        classDeclaration.Identifier.GetLocation(),
                        namedTypeSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }

                break;
            }
            // 检查3: Node类使用非AutoLoad模式
            case true when mode != SingletonMode.AutoLoad:
            {
                if (classDeclaration != null)
                {
                    var modeText = mode == SingletonMode.Lazy ? "Lazy" : "Eager";
                    var diagnostic = Diagnostic.Create(
                        AutoLoadRecommendedForNodeRule,
                        classDeclaration.Identifier.GetLocation(),
                        namedTypeSymbol.Name,
                        modeText);
                    context.ReportDiagnostic(diagnostic);
                }

                break;
            }
        }
    }
    
    private static ClassDeclarationSyntax? GetClassDeclaration(INamedTypeSymbol classSymbol, 
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var location in classSymbol.Locations)
        {
            if (location.SourceTree == null)
                continue;
                
            var root = location.SourceTree.GetRoot(cancellationToken);

            var node = root.FindNode(location.SourceSpan);
            
            var classDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDeclaration != null)
            {
                return classDeclaration;
            }
        }
        
        return null;
    }

    private static bool IsOrInheritsFromNode(INamedTypeSymbol typeSymbol)
    {
        while (true)
        {
            if (typeSymbol.BaseType == null) return false;
            var baseType = typeSymbol.BaseType;

            if (baseType.Name == "Node" && baseType.ContainingNamespace?.ToDisplayString() == "Godot")
            {
                return true;
            }

            typeSymbol = baseType;
        }
    }
}

internal enum SingletonMode
{
    Lazy,
    Eager,
    AutoLoad
}
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace GodotSimpleTools.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PartialClassRequiredAnalyzer : DiagnosticAnalyzer
{
    public const string GeneratorClassShouldBePartialId = "GST000";
    
    // 需要生成代码的特性列表
    private static readonly string[] GeneratorAttributes =
    [
        "NotifyAttribute",
        "Notify",  // 也检查短名称
        "NodePathAttribute",
        "NodePath"  // 也检查短名称
    ];

    private const string GeneratorClassShouldBePartialTitle = "使用了代码生成特性的类应该是partial类";

    private const string GeneratorClassShouldBePartialMessageFormat = "类'{0}'使用了代码生成特性[{1}]，需要标记为partial类";

    private const string Category = "Usage";
    
    // 诊断规则定义
    private static readonly DiagnosticDescriptor GeneratorClassShouldBePartialRule = new DiagnosticDescriptor(
        GeneratorClassShouldBePartialId,
        GeneratorClassShouldBePartialTitle,
        GeneratorClassShouldBePartialMessageFormat,
        Category,
        DiagnosticSeverity.Error,  // 错误级别，因为代码生成器需要partial类才能工作
        isEnabledByDefault: true,
        description: "使用了代码生成特性的类必须标记为partial，否则代码生成器无法生成代码.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(GeneratorClassShouldBePartialRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSymbolAction(AnalyzeClassSymbol, SymbolKind.NamedType);
    }

    private void AnalyzeClassSymbol(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        
        // 查找类中所有使用了需要代码生成的特性的成员
        var generatorAttributesUsed = new HashSet<string>();
        
        // 检查所有成员
        foreach (var attribute in namedTypeSymbol.GetMembers().Select(
                     member => member.GetAttributes()).SelectMany(attributes => attributes))
        {
            if (attribute.AttributeClass == null)
                continue;
                    
            var attributeName = attribute.AttributeClass.Name;
                
            // 检查是否是代码生成特性
            if (GeneratorAttributes.All(generatorAttribute => attributeName != generatorAttribute)) continue;
            var displayName = attributeName.Replace("Attribute", "");
            generatorAttributesUsed.Add(displayName);
        }
        
        // 如果没有使用任何代码生成特性，则不需要检查
        if (!generatorAttributesUsed.Any())
            return;
        
        // 查找类的声明语法
        var classDeclaration = GetClassDeclaration(namedTypeSymbol, context.CancellationToken);
        if (classDeclaration == null)
            return;
        
        // 检查类是否是partial类
        bool isPartialClass = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        
        // 生成诊断
        if (isPartialClass) return;
        // 将所有使用的特性名称连接成字符串
        var attributesString = string.Join(", ", generatorAttributesUsed.OrderBy(a => a));
            
        var diagnostic = Diagnostic.Create(
            GeneratorClassShouldBePartialRule,
            classDeclaration.Identifier.GetLocation(),
            namedTypeSymbol.Name,
            attributesString);
        context.ReportDiagnostic(diagnostic);
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
            
            // 查找ClassDeclarationSyntax
            var classDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDeclaration != null)
                return classDeclaration;
            
            // 如果在当前文件中找不到，可能在partial类的其他文件中
            // 在语法树中查找同名的类声明
            var declarations = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.Text == classSymbol.Name)
                .ToList();
                
            if (declarations.Any())
                return declarations.First();
        }
        
        return null;
    }
}
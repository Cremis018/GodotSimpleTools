using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace GodotSimpleTools.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NodePathAnalyzer : DiagnosticAnalyzer
{
    public const string InvalidNodePathClassId = "GST002";
    public const string InvalidNodePathTypeId = "GST003";
    public const string PrivatePropertyWarningId = "GST004";
    
    // 直接使用字符串，而不是LocalizableResourceString
    private static readonly LocalizableString InvalidNodePathClassTitle = "NodePathAttribute只能在Node派生类中使用";
    private static readonly LocalizableString InvalidNodePathClassMessageFormat = 
        "[NodePath]特性只能用在继承自Godot.Node的类中，但当前类'{0}'不是Node的派生类";
    
    private static readonly LocalizableString InvalidNodePathTypeTitle = "NodePathAttribute只能用于Node类型";
    private static readonly LocalizableString InvalidNodePathTypeMessageFormat = 
        "[NodePath]特性只能用于Godot.Node或其派生类型，但当前类型是'{0}'";
    
    private static readonly LocalizableString PrivatePropertyWarningTitle = "建议为NodePath属性添加private set";
    private static readonly LocalizableString PrivatePropertyWarningMessageFormat = 
        "建议为带有[NodePath]特性的属性'{0}'添加private set访问器以提高代码安全性";
    
    private const string Category = "Usage";
    
    // 诊断规则定义
    private static readonly DiagnosticDescriptor InvalidNodePathClassRule = new DiagnosticDescriptor(
        InvalidNodePathClassId,
        InvalidNodePathClassTitle,
        InvalidNodePathClassMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    private static readonly DiagnosticDescriptor InvalidNodePathTypeRule = new DiagnosticDescriptor(
        InvalidNodePathTypeId,
        InvalidNodePathTypeTitle,
        InvalidNodePathTypeMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    private static readonly DiagnosticDescriptor PrivatePropertyWarningRule = new DiagnosticDescriptor(
        PrivatePropertyWarningId,
        PrivatePropertyWarningTitle,
        PrivatePropertyWarningMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "建议为NodePath属性添加private set访问器以提高代码安全性.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(InvalidNodePathClassRule, InvalidNodePathTypeRule, PrivatePropertyWarningRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
        
        // 检查是否带有NodePathAttribute
        bool hasNodePath = false;
        foreach (var attributeList in fieldDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeSymbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
                if (attributeSymbol?.ContainingType?.Name == "NodePathAttribute")
                {
                    hasNodePath = true;
                    break;
                }
            }
            if (hasNodePath) break;
        }
        
        if (!hasNodePath) return;
        
        // 检查1: 类必须继承自Node
        var classDeclaration = GetContainingClass(fieldDeclaration);
        if (classDeclaration != null)
        {
            if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is { } classSymbol && !IsOrInheritsFromNode(classSymbol))
            {
                var diagnostic = Diagnostic.Create(
                    InvalidNodePathClassRule,
                    fieldDeclaration.GetLocation(),
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
        
        // 检查2: 字段类型必须是Node或其派生类型
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol) continue;
            var fieldType = fieldSymbol.Type;
            if (IsOrInheritsFromNode(fieldType)) continue;
            var diagnostic = Diagnostic.Create(
                InvalidNodePathTypeRule,
                fieldDeclaration.GetLocation(),
                fieldType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        
        // 检查是否带有NodePathAttribute
        bool hasNodePath = false;
        foreach (var attributeList in propertyDeclaration.AttributeLists)
        {
            if (attributeList.Attributes.Select(
                    attribute => context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol)
                .Any(attributeSymbol => attributeSymbol?.ContainingType?.Name == "NodePathAttribute"))
            {
                hasNodePath = true;
            }

            if (hasNodePath) break;
        }
        
        if (!hasNodePath) return;
        
        // 检查1: 类必须继承自Node
        var classDeclaration = GetContainingClass(propertyDeclaration);
        if (classDeclaration != null)
        {
            if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is { } classSymbol && !IsOrInheritsFromNode(classSymbol))
            {
                var diagnostic = Diagnostic.Create(
                    InvalidNodePathClassRule,
                    propertyDeclaration.GetLocation(),
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
        
        // 检查2: 属性类型必须是Node或其派生类型
        if (context.SemanticModel.GetDeclaredSymbol(propertyDeclaration) is not { } propertySymbol) return;
        {
            var propertyType = propertySymbol.Type;
            if (!IsOrInheritsFromNode(propertyType))
            {
                var diagnostic = Diagnostic.Create(
                    InvalidNodePathTypeRule,
                    propertyDeclaration.GetLocation(),
                    propertyType.Name);
                context.ReportDiagnostic(diagnostic);
            }
            
            // 检查3: 推荐属性有private set（改为警告）
            var accessorList = propertyDeclaration.AccessorList;
            if (accessorList == null) return;
            {
                bool hasPrivateSet = false;
                
                foreach (var unused in accessorList.Accessors.Where(
                             accessor => accessor.Kind() == SyntaxKind.SetAccessorDeclaration)
                             .Where(accessor => accessor.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))))
                {
                    hasPrivateSet = true;
                }

                if (hasPrivateSet) return;
                var diagnostic = Diagnostic.Create(
                    PrivatePropertyWarningRule,
                    propertyDeclaration.GetLocation(),
                    propertySymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static ClassDeclarationSyntax? GetContainingClass(SyntaxNode node)
    {
        while (node != null)
        {
            if (node is ClassDeclarationSyntax classDeclaration)
            {
                return classDeclaration;
            }
            node = node.Parent;
        }
        return null;
    }
    
    private static bool IsOrInheritsFromNode(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
            return false;
        
        // 检查是否直接是Node类型
        if (namedTypeSymbol.Name == "Node" && 
            namedTypeSymbol.ContainingNamespace?.ToDisplayString() == "Godot")
            return true;
        
        // 递归检查基类
        var baseType = namedTypeSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "Node" && 
                baseType.ContainingNamespace?.ToDisplayString() == "Godot")
                return true;
            baseType = baseType.BaseType;
        }
        
        return false;
    }
}
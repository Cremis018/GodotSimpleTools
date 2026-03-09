using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace GodotSimpleTools.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NotifyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GST001";
    
    private static readonly LocalizableString Title = "Notify特性使用错误";
    private static readonly LocalizableString MessageFormat = "{0}";
    private const string Category = "Usage";
    
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, 
        Title, 
        MessageFormat, 
        Category, 
        DiagnosticSeverity.Error, 
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

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
        
        // 检查是否带有NotifyAttribute
        bool hasNotify = false;
        foreach (var attributeList in fieldDeclaration.AttributeLists)
        {
            if (attributeList.Attributes.Select(
                    attribute => context.SemanticModel.GetSymbolInfo(
                        attribute).Symbol as IMethodSymbol).Any(
                    attributeSymbol => attributeSymbol?.ContainingType?.Name == "NotifyAttribute"))
            {
                hasNotify = true;
            }

            if (hasNotify) break;
        }
        
        if (!hasNotify) return;
        
        // 检查1: 字段必须是私有的
        if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                fieldDeclaration.GetLocation(),
                "带有[Notify]特性的字段必须是私有的");
            context.ReportDiagnostic(diagnostic);
        }
        
        // 检查2: 字段不能是静态的
        if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                fieldDeclaration.GetLocation(),
                "带有[Notify]特性的字段不能是静态的");
            context.ReportDiagnostic(diagnostic);
        }
        
        // 检查 3: 字段类型必须是可空或可比较的
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol) continue;
            var type = fieldSymbol.Type;
                    
            // 值类型需要检查是否支持 Equals 比较
            if (!type.IsValueType) continue;
            // 检查类型是否实现了 IEquatable<T> 或重写了 Equals 方法
            bool supportsEquality = false;
                        
            // 检查是否实现了 IEquatable<T>
            if (type.AllInterfaces.Any(i => i.ConstructedFrom.Name == "IEquatable<T>"))
            {
                supportsEquality = true;
            }
            // 检查是否重写了 Equals 方法
            else if (type.GetMembers("Equals").Any(m => m.IsOverride))
            {
                supportsEquality = true;
            }
            // 基本值类型（如 int, float, bool 等）默认支持 Equals
            else if (type.SpecialType != SpecialType.None)
            {
                supportsEquality = true;
            }

            if (supportsEquality) continue;
            var diagnostic = Diagnostic.Create(
                Rule,
                fieldDeclaration.GetLocation(),
                $"带有 [Notify] 特性的值类型字段 '{fieldSymbol.Name}' 必须实现 IEquatable<T> 或重写 Equals 方法以支持相等性比较");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        
        // 检查是否带有NotifyAttribute
        bool hasNotify = false;
        foreach (var attributeList in propertyDeclaration.AttributeLists)
        {
            if (attributeList.Attributes.Select(attribute => context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol).Any(attributeSymbol => attributeSymbol?.ContainingType?.Name == "NotifyAttribute"))
            {
                hasNotify = true;
            }

            if (hasNotify) break;
        }
        
        if (!hasNotify) return;
        
        // 检查1: 属性必须具有getter和setter
        var accessorList = propertyDeclaration.AccessorList;
        if (accessorList == null)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                "带有[Notify]特性的属性必须具有getter和setter");
            context.ReportDiagnostic(diagnostic);
            return;
        }
        
        bool hasGetter = false;
        bool hasSetter = false;
        
        foreach (var accessor in accessorList.Accessors)
        {
            if (accessor.Kind() == SyntaxKind.GetAccessorDeclaration)
                hasGetter = true;
            if (accessor.Kind() == SyntaxKind.SetAccessorDeclaration)
                hasSetter = true;
        }
        
        if (!hasGetter || !hasSetter)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                "带有[Notify]特性的属性必须同时具有getter和setter");
            context.ReportDiagnostic(diagnostic);
        }
        
        // 检查2: 属性不能是抽象的
        if (propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                "带有[Notify]特性的属性不能是抽象的");
            context.ReportDiagnostic(diagnostic);
        }
        
        // 检查3: 属性不能是静态的
        if (propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                "带有[Notify]特性的属性不能是静态的");
            context.ReportDiagnostic(diagnostic);
        }
        
        // 检查4: 确保Value参数是有效的（针对属性）
        foreach (var attribute in from attributeList in propertyDeclaration.AttributeLists from attribute in attributeList.Attributes let attributeSymbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol where attributeSymbol?.ContainingType?.Name == "NotifyAttribute" select attribute)
        {
            // 检查Value参数是否与属性类型兼容
            if (context.SemanticModel.GetDeclaredSymbol(propertyDeclaration) is not { } propertySymbol) continue;
            var attributeData = propertySymbol.GetAttributes()
                .First(ad => ad.AttributeClass?.Name == "NotifyAttribute");

            if (attributeData.ConstructorArguments.Length <= 0) continue;
            foreach (var arg in attributeData.ConstructorArguments)
            {
                if (arg.Kind != TypedConstantKind.Primitive ||
                    arg.Type?.Name != "String" ||
                    arg.Value == null) continue;
                var valueStr = arg.Value.ToString();
                if (IsValidDefaultValue(valueStr, propertySymbol.Type)) continue;
                var diagnostic = Diagnostic.Create(
                    Rule,
                    attribute.GetLocation(),
                    $"默认值'{valueStr}'与属性类型'{propertySymbol.Type.Name}'不兼容");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private bool IsValidDefaultValue(string valueStr, ITypeSymbol typeSymbol)
    {
        // 简化的类型兼容性检查
        // 实际实现需要更复杂的类型转换检查
        var typeName = typeSymbol.Name;

        return typeName switch
        {
            "Int32" => int.TryParse(valueStr, out _),
            "Single" or "Float" => float.TryParse(valueStr, out _),
            "Double" => double.TryParse(valueStr, out _),
            "Boolean" => bool.TryParse(valueStr, out _),
            "String" => true // 字符串总是有效
            ,
            _ => true
        };
    }
}
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Linq;

namespace GodotSimpleTools.Generators;

[Generator]
public class NotifyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 收集所有带有NotifyAttribute的符号
        var notifySymbolsProvider = context.SyntaxProvider
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
                            var notifyFields = new List<(IFieldSymbol, SyntaxTokenList)>();
                            
                            foreach (var variable in fieldSyntax.Declaration.Variables)
                            {
                                if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol
                                    fieldSymbol) continue;
                                var attributes = fieldSymbol.GetAttributes();
                                var notifyAttribute = attributes.FirstOrDefault(ad =>
                                    ad.AttributeClass != null && 
                                    SymbolEqualityComparer.Default.Equals(
                                        ad.AttributeClass, 
                                        generatorSyntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("GodotSimpleTools.NotifyAttribute")) ||
                                    ad.AttributeClass?.Name == "NotifyAttribute");
                                    
                                if (notifyAttribute != null) notifyFields.Add((fieldSymbol, fieldSyntax.Modifiers));
                            }

                            if (notifyFields.Any())
                                return (ClassSyntax: node.Parent as ClassDeclarationSyntax, 
                                    NotifyFields: notifyFields,
                                    NotifyProperties: new List<(IPropertySymbol, SyntaxTokenList)>());
                            break;
                        }
                        case PropertyDeclarationSyntax propertySyntax:
                        {
                            if (semanticModel.GetDeclaredSymbol(propertySyntax, cancellationToken) is { } propertySymbol)
                            {
                                var attributes = propertySymbol.GetAttributes();
                                var notifyAttribute = attributes.FirstOrDefault(ad =>
                                    ad.AttributeClass != null && 
                                    SymbolEqualityComparer.Default.Equals(
                                        ad.AttributeClass, 
                                        generatorSyntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("GodotSimpleTools.NotifyAttribute")) ||
                                    ad.AttributeClass?.Name == "NotifyAttribute");
                                
                                if (notifyAttribute != null)
                                {
                                    var notifyProperties = new List<(IPropertySymbol, SyntaxTokenList)>
                                    {
                                        (propertySymbol, propertySyntax.Modifiers)
                                    };
                                
                                    return (ClassSyntax: node.Parent as ClassDeclarationSyntax,
                                            NotifyFields: [],
                                            NotifyProperties: notifyProperties);
                                }
                            }
                            break;
                        }
                    }
                    
                    return (ClassSyntax: null, 
                            NotifyFields: [],
                            NotifyProperties: []);
                })
            .Where(static x => x.ClassSyntax != null)
            .Collect();

        // 2. 按类分组 - 修复语义模型获取问题
        var groupedByClassProvider = notifySymbolsProvider
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) =>
            {
                var (symbols, compilation) = pair;
                var grouped = new Dictionary<INamedTypeSymbol, 
                    (List<(IFieldSymbol, SyntaxTokenList)>, List<(IPropertySymbol, SyntaxTokenList)>)>(SymbolEqualityComparer.Default);
                
                foreach (var (classSyntax, notifyFields, notifyProperties) in symbols)
                {
                    if (classSyntax == null) continue;
                    
                    // 通过compilation获取语义模型
                    var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

                    if (semanticModel.GetDeclaredSymbol(classSyntax) is not { } classSymbol) continue;
                    
                    if (!grouped.TryGetValue(classSymbol, out var members))
                    {
                        members = ([], []);
                        grouped[classSymbol] = members;
                    }
                    
                    members.Item1.AddRange(notifyFields);
                    members.Item2.AddRange(notifyProperties);
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
                    var notifyFields = kvp.Value.Item1;
                    var notifyProperties = kvp.Value.Item2;
                            
                    try
                    {
                        var sourceCode = GeneratePartialClass(classSymbol,
                            notifyFields,
                            notifyProperties);
                        var hintName = $"{classSymbol.ToDisplayString().Replace(".", "_")}.GST.g.cs";
        
                        sourceProductionContext.AddSource(hintName,
                            SourceText.From(sourceCode,
                                Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        // 可以记录错误，但不中断编译
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "GSTGEN001",
                                "代码生成错误",
                                $"生成{classSymbol.Name}的代码时出错：{ex.Message}",
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
        List<(IFieldSymbol Field, SyntaxTokenList Modifiers)> notifyFields,
        List<(IPropertySymbol Property, SyntaxTokenList Modifiers)> notifyProperties)
    {
        var sb = new StringBuilder();
        
        // 命名空间
        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine($"namespace {classSymbol.ContainingNamespace.ToDisplayString()};");
            sb.AppendLine();
        }
        
        // 注释
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("// 由GodotSimpleTools生成器生成");
        sb.AppendLine($"public partial class {classSymbol.Name}");
        sb.AppendLine("{");
        
        // 为每个字段生成代码
        foreach (var (field, modifiers) in notifyFields)
        {
            if (!IsValidField(modifiers)) continue;
            
            var generatedCode = GenerateForField(field);
            if (string.IsNullOrEmpty(generatedCode)) continue;
            sb.AppendLine(generatedCode);
            sb.AppendLine(); // 添加空行分隔
        }
        
        // 为每个属性生成代码
        foreach (var (property, modifiers) in notifyProperties)
        {
            if (!IsValidProperty(property, modifiers)) continue;
            
            var generatedCode = GenerateForProperty(property);
            if (string.IsNullOrEmpty(generatedCode)) continue;
            sb.AppendLine(generatedCode);
            sb.AppendLine(); // 添加空行分隔
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private static bool IsValidField(SyntaxTokenList modifiers)
    {
        // 检查字段是否私有
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
            return false;

        // 检查字段是否静态
        return !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
    }
    
    private static bool IsValidProperty(IPropertySymbol propertySymbol, SyntaxTokenList modifiers)
    {
        // 检查属性是否抽象
        if (modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
            return false;
        
        // 检查属性是否静态
        if (modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            return false;

        // 检查是否有getter和setter
        return propertySymbol is { GetMethod: not null, SetMethod: not null };
    }

    private static string GenerateForField(IFieldSymbol fieldSymbol)
    {
        var notifyAttribute = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => 
                ad.AttributeClass != null && 
                SymbolEqualityComparer.Default.Equals(
                    ad.AttributeClass, 
                    fieldSymbol.ContainingAssembly.GetTypeByMetadataName("GodotSimpleTools.NotifyAttribute")) ||
                ad.AttributeClass?.Name == "NotifyAttribute");
        
        if (notifyAttribute == null) return string.Empty;
        
        var fieldName = fieldSymbol.Name;
        var propertyName = GetPropertyNameFromField(fieldName);
        var typeName = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var containingTypeName = fieldSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        
        // 解析特性参数
        var returnMode = NotifyReturnMode.NoReturn;
        var hasChanging = false;
        
        if (notifyAttribute.ConstructorArguments.Length > 0)
        {
            foreach (var arg in notifyAttribute.ConstructorArguments)
            {
                switch (arg)
                {
                    case { Kind: TypedConstantKind.Enum, Type: not null } when
                        arg.Type.ToDisplayString().Contains("NotifyReturnMode"):
                    {
                        if (int.TryParse(arg.Value?.ToString(), out var enumValue))
                        {
                            returnMode = (NotifyReturnMode)enumValue;
                        }

                        break;
                    }
                    case { Kind: TypedConstantKind.Primitive, Type.SpecialType: SpecialType.System_Boolean, Value: bool boolValue }:
                        hasChanging = boolValue;
                        break;
                }
            }
        }
        
        // 检查命名参数
        foreach (var arg in notifyAttribute.NamedArguments)
        {
            switch (arg)
            {
                case { Key: "ReturnMode", Value.Value: int returnModeValue }:
                    returnMode = (NotifyReturnMode)returnModeValue;
                    break;
                case { Key: "HasChanging", Value.Value: bool hasChangingValue }:
                    hasChanging = hasChangingValue;
                    break;
            }
        }
        
        var sb = new StringBuilder();
        
        // 生成属性
        sb.AppendLine($"    public {typeName} {propertyName} {{ get => Get{propertyName}(); set => Set{propertyName}(value); }}");
        sb.AppendLine($"    protected global::System.Action? _set{propertyName}Callback;");
        
        // 生成事件
        string eventType = returnMode switch
        {
            NotifyReturnMode.NoReturn => "global::System.Action?",
            NotifyReturnMode.ReturnValue => $"global::System.Action<{typeName}>?",
            NotifyReturnMode.ReturnSelf => $"global::System.Action<{containingTypeName}>?",
            _ => "global::System.Action?"
        };
        
        sb.AppendLine($"    public event {eventType} {propertyName}Changed;");
        
        if (hasChanging && returnMode == NotifyReturnMode.ReturnSelf)
        {
            sb.AppendLine($"    public event global::System.Action<{containingTypeName}>? {propertyName}Changing;");
        }
        
        // 生成Get方法
        sb.AppendLine($"    public {typeName} Get{propertyName}() => {fieldName};");
        
        // 生成Set方法
        sb.AppendLine($"    public void Set{propertyName}({typeName} value)");
        sb.AppendLine("    {");
        sb.AppendLine($"        _set{propertyName}Callback?.Invoke();");
        
        if (hasChanging && returnMode == NotifyReturnMode.ReturnSelf)
        {
            sb.AppendLine($"        {propertyName}Changing?.Invoke(this);");
        }
        
        sb.AppendLine($"        if (global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals({fieldName}, value)) return;");
        sb.AppendLine($"        {fieldName} = value;");
        
        switch (returnMode)
        {
            case NotifyReturnMode.NoReturn:
                sb.AppendLine($"        {propertyName}Changed?.Invoke();");
                break;
            case NotifyReturnMode.ReturnValue:
                sb.AppendLine($"        {propertyName}Changed?.Invoke(value);");
                break;
            case NotifyReturnMode.ReturnSelf:
                sb.AppendLine($"        {propertyName}Changed?.Invoke(this);");
                break;
        }
        
        sb.AppendLine("    }");
        
        return sb.ToString();
    }

    private static string GenerateForProperty(IPropertySymbol propertySymbol)
    {
        var notifyAttribute = propertySymbol.GetAttributes()
            .FirstOrDefault(ad => 
                ad.AttributeClass != null && 
                SymbolEqualityComparer.Default.Equals(
                    ad.AttributeClass, 
                    propertySymbol.ContainingAssembly.GetTypeByMetadataName("GodotSimpleTools.NotifyAttribute")) ||
                ad.AttributeClass?.Name == "NotifyAttribute");
        
        if (notifyAttribute == null) return string.Empty;
        
        var propertyName = propertySymbol.Name;
        var fieldName = GetFieldNameFromProperty(propertyName);
        var typeName = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        
        // 解析默认值
        string defaultValue = "default";
        var valueParam = notifyAttribute.ConstructorArguments.FirstOrDefault(arg => 
            arg is { Kind: TypedConstantKind.Primitive, Type.SpecialType: SpecialType.System_String });
        
        if (valueParam.Value != null)
        {
            defaultValue = valueParam.Value.ToString()!;
            
            // 尝试转换为正确的类型
            if (TryConvertDefaultValue(defaultValue, propertySymbol.Type, out var convertedValue))
            {
                defaultValue = convertedValue;
            }
        }
        
        // 检查命名参数
        foreach (var arg in notifyAttribute.NamedArguments)
        {
            if (arg is not { Key: "Value", Value.Value: not null }) continue;
            defaultValue = arg.Value.Value.ToString()!;
            if (TryConvertDefaultValue(defaultValue, propertySymbol.Type, out var convertedValue))
            {
                defaultValue = convertedValue;
            }
        }
        
        var sb = new StringBuilder();
        
        // 生成字段
        sb.AppendLine($"    private {typeName} {fieldName} = {defaultValue};");
        
        // 生成事件
        sb.AppendLine($"    public event global::System.Action? {propertyName}Changed;");
        
        // 生成Get/Set方法
        sb.AppendLine($"    public {typeName} Get{propertyName}() => {fieldName};");
        sb.AppendLine($"    public void Set{propertyName}({typeName} value, global::System.Action? callback = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        callback?.Invoke();");
        sb.AppendLine($"        if (global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals({fieldName}, value)) return;");
        sb.AppendLine($"        {fieldName} = value;");
        sb.AppendLine($"        {propertyName}Changed?.Invoke();");
        sb.AppendLine("    }");
        
        return sb.ToString();
    }
    
    private static string GetPropertyNameFromField(string fieldName)
    {
        // 移除下划线前缀
        if (fieldName.StartsWith("_") && fieldName.Length > 1) fieldName = fieldName.Substring(1);
        
        // 首字母大写
        return char.ToUpper(fieldName[0]) + fieldName.Substring(1);
    }
    
    private static string GetFieldNameFromProperty(string propertyName)
    {
        // 添加下划线前缀，首字母小写
        return "_" + char.ToLower(propertyName[0]) + propertyName.Substring(1);
    }
    
    private static bool TryConvertDefaultValue(string valueStr, ITypeSymbol typeSymbol, out string result)
    {
        result = valueStr;
        
        try
        {
            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Int32:
                    if (int.TryParse(valueStr, out _))
                    {
                        return true;
                    }
                    break;
                    
                case SpecialType.System_Single:
                    if (float.TryParse(valueStr, out var floatValue))
                    {
                        result = $"{floatValue}f";
                        return true;
                    }
                    break;
                    
                case SpecialType.System_Double:
                    if (double.TryParse(valueStr, out var doubleValue))
                    {
                        result = doubleValue.ToString("R");
                        return true;
                    }
                    break;
                    
                case SpecialType.System_Boolean:
                    if (bool.TryParse(valueStr, out var boolValue))
                    {
                        result = boolValue.ToString().ToLower();
                        return true;
                    }
                    break;
                    
                case SpecialType.System_String:
                    result = $"\"{valueStr.Replace("\"", "\\\"")}\"";
                    return true;
                    
                default:
                    // 对于其他类型，使用原始字符串
                    if (typeSymbol is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated })
                    {
                        result = "null";
                        return true;
                    }
                    break;
            }
        }
        catch
        {
            // 转换失败，使用原始值
        }
        
        return false;
    }
}

// 需要与主库中的NotifyReturnMode枚举保持一致
internal enum NotifyReturnMode
{
    NoReturn,
    ReturnValue,
    ReturnSelf
}
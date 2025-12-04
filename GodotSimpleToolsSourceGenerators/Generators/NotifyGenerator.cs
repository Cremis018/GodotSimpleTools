using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSimpleToolsSourceGenerators.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class NotifyGenerator : IIncrementalGenerator
{
    private const string NotifyAttributeName = "GodotSimpleTools.NotifyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var notifyProperties = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NotifyAttributeName,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (attributeContext, cancellationToken) =>
                    GetPropertyInfo(attributeContext, cancellationToken))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedProperties = notifyProperties.Collect();

        context.RegisterSourceOutput(
            collectedProperties,
            static (sourceProductionContext, properties) =>
            {
                if (properties.IsDefaultOrEmpty)
                {
                    return;
                }

                GenerateSource(sourceProductionContext, properties);
            });
    }

    private static NotifyPropertyInfo? GetPropertyInfo(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNode is not PropertyDeclarationSyntax)
        {
            return null;
        }

        if (context.TargetSymbol is not IPropertySymbol propertySymbol)
        {
            return null;
        }

        if (propertySymbol.ContainingType is not { } containingType)
        {
            return null;
        }

        if (!IsPartial(containingType, cancellationToken))
        {
            return null;
        }

        var attributeData = context.Attributes.FirstOrDefault(
            static attribute => attribute.AttributeClass?.ToDisplayString() == NotifyAttributeName);

        if (attributeData is null)
        {
            return null;
        }

        var hasChanging = GetBoolNamedArgument(attributeData, "HasChanging");
        var defaultValueLiteral = GetDefaultValueLiteral(attributeData);

        return new NotifyPropertyInfo(
            containingType,
            propertySymbol.Name,
            propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            hasChanging,
            defaultValueLiteral);
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(cancellationToken) is TypeDeclarationSyntax typeDeclaration &&
                typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GetBoolNamedArgument(AttributeData attributeData, string argumentName)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == argumentName && namedArgument.Value.Value is bool boolValue)
            {
                return boolValue;
            }
        }

        return false;
    }

    private static string? GetDefaultValueLiteral(AttributeData attributeData)
    {
        // 首先检查位置参数 (构造函数的第一个参数)
        if (attributeData.ConstructorArguments.Length > 0)
        {
            var firstArg = attributeData.ConstructorArguments[0];
            if (firstArg.Value != null)
            {
                return firstArg.ToCSharpString();
            }
        }

        // 然后检查命名参数
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "DefaultValue")
            {
                return namedArgument.Value.ToCSharpString();
            }
        }

        return null;
    }


    private static void GenerateSource(
        SourceProductionContext context,
        ImmutableArray<NotifyPropertyInfo> properties)
    {
        var groups = properties
            .GroupBy(static property => property.ContainingType, SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            if (group.Key is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            var source = BuildClassSource(classSymbol, group.ToArray());
            var hintName = $"{group.Key.Name}_NotifyGenerator.g.cs";
            context.AddSource(hintName, source);
        }
    }

    private static string BuildClassSource(
        INamedTypeSymbol classSymbol,
        IReadOnlyList<NotifyPropertyInfo> properties)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable disable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();

        var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("public partial ").Append(GetTypeDeclaration(classSymbol)).AppendLine();
        builder.AppendLine("{");

        builder.AppendLine();

        foreach (var property in properties)
        {
            WriteField(builder, property);
        }

        builder.AppendLine();

        foreach (var property in properties)
        {
            WriteEvents(builder, property);
        }

        builder.AppendLine();

        foreach (var property in properties)
        {
            WriteAccessors(builder, property);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GetTypeDeclaration(INamedTypeSymbol classSymbol)
    {
        var typeKeyword = classSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";

        var typeParameters = classSymbol.TypeParameters.Length > 0
            ? $"<{string.Join(", ", classSymbol.TypeParameters.Select(parameter => parameter.Name))}>"
            : string.Empty;

        var constraints = BuildTypeParameterConstraints(classSymbol);

        return $"{typeKeyword} {classSymbol.Name}{typeParameters}{constraints}";
    }

    private static string BuildTypeParameterConstraints(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var parameter in classSymbol.TypeParameters)
        {
            var clauses = new List<string>();

            if (parameter.HasReferenceTypeConstraint)
            {
                clauses.Add("class");
            }
            else if (parameter.HasValueTypeConstraint)
            {
                clauses.Add("struct");
            }

            if (parameter.HasNotNullConstraint)
            {
                clauses.Add("notnull");
            }

            if (parameter.HasUnmanagedTypeConstraint)
            {
                clauses.Add("unmanaged");
            }

            clauses.AddRange(parameter.ConstraintTypes.Select(
                type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

            if (parameter.HasConstructorConstraint)
            {
                clauses.Add("new()");
            }

            if (clauses.Count == 0)
            {
                continue;
            }

            builder.Append(" where ")
                .Append(parameter.Name)
                .Append(" : ")
                .Append(string.Join(", ", clauses));
        }

        return builder.ToString();
    }

    private static void WriteField(StringBuilder builder, NotifyPropertyInfo property)
    {
        var fieldName = GetFieldName(property.PropertyName);
        builder.Append("    private ")
            .Append(property.PropertyType)
            .Append(' ')
            .Append(fieldName);

        if (!string.IsNullOrWhiteSpace(property.DefaultValueLiteral))
        {
            builder.Append(" = ").Append(property.DefaultValueLiteral);
        }

        builder.AppendLine(";");
    }

    private static void WriteEvents(StringBuilder builder, NotifyPropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var propertyName = property.PropertyName;

        builder.Append("    public event Action<")
            .Append(propertyType)
            .Append("> ")
            .Append(propertyName)
            .AppendLine("Changed;");

        if (property.HasChanging)
        {
            builder.Append("    public event Action<")
                .Append(propertyType)
                .Append("> ")
                .Append(propertyName)
                .AppendLine("Changing;");
        }

        builder.AppendLine();
    }

    private static void WriteAccessors(StringBuilder builder, NotifyPropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var propertyName = property.PropertyName;
        var fieldName = GetFieldName(propertyName);

        builder.Append("    public ")
            .Append(propertyType)
            .Append(" Get")
            .Append(propertyName)
            .Append("() => ")
            .Append(fieldName)
            .AppendLine(";");

        builder.Append("    public void Set")
            .Append(propertyName)
            .Append("(")
            .Append(propertyType)
            .AppendLine(" value, Action? callback = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        callback?.Invoke();");

        if (property.HasChanging)
        {
            builder.Append("        ")
                .Append(propertyName)
                .AppendLine("Changing?.Invoke(value);");
        }

        builder.Append("        if (EqualityComparer<")
            .Append(propertyType)
            .Append(">.Default.Equals(")
            .Append(fieldName)
            .AppendLine(", value))");
        builder.AppendLine("        {");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.Append("        ")
            .Append(fieldName)
            .AppendLine(" = value;");
        builder.Append("        ")
            .Append(propertyName)
            .AppendLine("Changed?.Invoke(value);");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string GetFieldName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return "_value";
        }

        var suffix = propertyName.Length > 1 ? propertyName.Substring(1) : string.Empty;
        return "_" + char.ToLowerInvariant(propertyName[0]) + suffix;
    }

    private sealed class NotifyPropertyInfo(
        INamedTypeSymbol containingType,
        string propertyName,
        string propertyType,
        bool hasChanging,
        string? defaultValueLiteral)
    {
        public INamedTypeSymbol ContainingType { get; } = containingType;
        public string PropertyName { get; } = propertyName;
        public string PropertyType { get; } = propertyType;
        public bool HasChanging { get; } = hasChanging;
        public string? DefaultValueLiteral { get; } = defaultValueLiteral;
    }
}
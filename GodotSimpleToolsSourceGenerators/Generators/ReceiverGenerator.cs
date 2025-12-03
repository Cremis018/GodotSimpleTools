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
public sealed class ReceiverGenerator : IIncrementalGenerator
{
    private const string ReceiverAttributeName = "GodotSimpleTools.Attributes.ReceiverAttribute";

    // 与 GodotSimpleTools.Attributes.NotifyMethod 枚举值保持一致
    private const int NotifyMethodChanged = 0;
    private const int NotifyMethodChanging = 1;
    private const int NotifyMethodAll = 2;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var receiverMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ReceiverAttributeName,
                static (node, _) => node is MethodDeclarationSyntax,
                static (attributeContext, cancellationToken) =>
                    GetReceiverMethodInfo(attributeContext, cancellationToken))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var collectedMethods = receiverMethods.Collect();

        context.RegisterSourceOutput(
            collectedMethods,
            static (sourceProductionContext, methods) =>
            {
                if (methods.IsDefaultOrEmpty)
                {
                    return;
                }

                GenerateSource(sourceProductionContext, methods);
            });
    }

    private static ReceiverMethodInfo? GetReceiverMethodInfo(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNode is not MethodDeclarationSyntax)
        {
            return null;
        }

        if (context.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        if (methodSymbol.ContainingType is not { } containingType)
        {
            return null;
        }

        if (!IsPartial(containingType, cancellationToken))
        {
            return null;
        }

        var receiverInfos = new List<ReceiverInfo>();

        foreach (var attributeData in context.Attributes)
        {
            if (attributeData.AttributeClass?.ToDisplayString() != ReceiverAttributeName)
            {
                continue;
            }

            var notifyClassName = GetStringConstructorArgument(
                attributeData,
                0,
                context.SemanticModel,
                cancellationToken);

            var notifyMemberName = GetStringConstructorArgument(
                attributeData,
                1,
                context.SemanticModel,
                cancellationToken);
            var method = GetNotifyMethod(attributeData, 2);

            if (string.IsNullOrWhiteSpace(notifyClassName) || string.IsNullOrWhiteSpace(notifyMemberName))
            {
                continue;
            }

            receiverInfos.Add(new ReceiverInfo(notifyClassName!, notifyMemberName!, method));
        }

        if (receiverInfos.Count == 0)
        {
            return null;
        }

        return new ReceiverMethodInfo(
            containingType,
            methodSymbol.Name,
            receiverInfos.ToImmutableArray());
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

    private static string? GetStringConstructorArgument(
        AttributeData attributeData,
        int index,
        SemanticModel? semanticModel,
        CancellationToken cancellationToken)
    {
        if (index < attributeData.ConstructorArguments.Length)
        {
            var argument = attributeData.ConstructorArguments[index];
            if (argument.Value is string directValue)
            {
                return directValue;
            }
        }

        if (semanticModel is not null &&
            attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax attributeSyntax &&
            attributeSyntax.ArgumentList?.Arguments.Count > index)
        {
            var syntaxArgument = attributeSyntax.ArgumentList.Arguments[index];
            var expression = syntaxArgument.Expression;

            var constantValue = semanticModel.GetConstantValue(expression);
            if (constantValue.HasValue && constantValue.Value is string constStringValue)
            {
                return constStringValue;
            }

            if (TryResolveNameof(expression) is { Length: > 0 } nameofValue)
            {
                return nameofValue;
            }

            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.ToString();
            }

            if (expression is IdentifierNameSyntax identifierNameSyntax)
            {
                return identifierNameSyntax.Identifier.ValueText;
            }

            return expression.ToString();
        }

        if (index < attributeData.ConstructorArguments.Length)
        {
            var argument = attributeData.ConstructorArguments[index];
            if (argument.Value is string fallbackString)
            {
                return fallbackString;
            }

            return argument.Value?.ToString();
        }

        return null;
    }

    private static int GetNotifyMethod(AttributeData attributeData, int index)
    {
        if (index >= attributeData.ConstructorArguments.Length)
        {
            return NotifyMethodChanged;
        }

        var argument = attributeData.ConstructorArguments[index];
        var value = argument.Value;
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is Enum enumValue)
        {
            return Convert.ToInt32(enumValue);
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToInt32(null);
            }
            catch
            {
                // ignore conversion failures and fall back to default
            }
        }

        return NotifyMethodChanged;
    }

    private static string? TryResolveNameof(ExpressionSyntax expression)
    {
        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.ValueText == "nameof" &&
            invocation.ArgumentList.Arguments.Count > 0)
        {
            var nameofArgument = invocation.ArgumentList.Arguments[0].Expression;
            return nameofArgument switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess when memberAccess.Name is IdentifierNameSyntax nameSyntax
                    => nameSyntax.Identifier.ValueText,
                _ => nameofArgument.ToString()
            };
        }

        return null;
    }

    private static void GenerateSource(
        SourceProductionContext context,
        ImmutableArray<ReceiverMethodInfo> methods)
    {
        var groups = methods
            .GroupBy(static method => method.ContainingType, SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            if (group.Key is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            var source = BuildClassSource(classSymbol, group.ToArray());
            var hintName = $"{classSymbol.Name}_ReceiverGenerator.g.cs";
            context.AddSource(hintName, source);
        }
    }

    private static string BuildClassSource(
        INamedTypeSymbol classSymbol,
        IReadOnlyList<ReceiverMethodInfo> methods)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
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

        WriteInitNotifies(builder, methods);
        builder.AppendLine();
        WriteDestroyNotifies(builder, methods);

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

    private static void WriteInitNotifies(
        StringBuilder builder,
        IReadOnlyList<ReceiverMethodInfo> methods)
    {
        builder.AppendLine("    public void InitNotifies()");
        builder.AppendLine("    {");

        foreach (var method in methods)
        {
            foreach (var receiverInfo in method.ReceiverInfos)
            {
                var propertyName = ExtractPropertyName(receiverInfo.NotifyMemberName);
                if (propertyName is null)
                {
                    continue;
                }

                foreach (var eventName in GetEventNames(propertyName, receiverInfo.Method))
                {
                    builder.Append("        ")
                        .Append(receiverInfo.NotifyClassName)
                        .Append('.')
                        .Append(eventName)
                        .Append(" += ")
                        .Append(method.MethodName)
                        .AppendLine(";");
                }
            }
        }

        builder.AppendLine("    }");
    }

    private static void WriteDestroyNotifies(
        StringBuilder builder,
        IReadOnlyList<ReceiverMethodInfo> methods)
    {
        builder.AppendLine("    public void DestroyNotifies()");
        builder.AppendLine("    {");

        foreach (var method in methods)
        {
            foreach (var receiverInfo in method.ReceiverInfos)
            {
                var propertyName = ExtractPropertyName(receiverInfo.NotifyMemberName);
                if (propertyName is null)
                {
                    continue;
                }

                foreach (var eventName in GetEventNames(propertyName, receiverInfo.Method))
                {
                    builder.Append("        ")
                        .Append(receiverInfo.NotifyClassName)
                        .Append('.')
                        .Append(eventName)
                        .Append(" -= ")
                        .Append(method.MethodName)
                        .AppendLine(";");
                }
            }
        }

        builder.AppendLine("    }");
    }

    private static string? ExtractPropertyName(string notifyMemberName)
    {
        if (string.IsNullOrWhiteSpace(notifyMemberName))
        {
            return null;
        }

        var lastDotIndex = notifyMemberName.LastIndexOf('.');
        var namePart = lastDotIndex >= 0 && lastDotIndex < notifyMemberName.Length - 1
            ? notifyMemberName.Substring(lastDotIndex + 1)
            : notifyMemberName;

        const string suffix = "_name";
        if (namePart.EndsWith(suffix, StringComparison.Ordinal))
        {
            return namePart.Substring(0, namePart.Length - suffix.Length);
        }

        return namePart;
    }

    private static IEnumerable<string> GetEventNames(string propertyName, int method)
    {
        switch (method)
        {
            case NotifyMethodChanged:
                yield return $"{propertyName}Changed";
                break;
            case NotifyMethodChanging:
                yield return $"{propertyName}Changing";
                break;
            case NotifyMethodAll:
                yield return $"{propertyName}Changed";
                yield return $"{propertyName}Changing";
                break;
        }
    }

    private sealed class ReceiverMethodInfo(
        INamedTypeSymbol containingType,
        string methodName,
        ImmutableArray<ReceiverInfo> receiverInfos)
    {
        public INamedTypeSymbol ContainingType { get; } = containingType;
        public string MethodName { get; } = methodName;
        public ImmutableArray<ReceiverInfo> ReceiverInfos { get; } = receiverInfos;
    }

    private sealed class ReceiverInfo(
        string notifyClassName,
        string notifyMemberName,
        int method)
    {
        public string NotifyClassName { get; } = notifyClassName;
        public string NotifyMemberName { get; } = notifyMemberName;
        public int Method { get; } = method;
    }
}
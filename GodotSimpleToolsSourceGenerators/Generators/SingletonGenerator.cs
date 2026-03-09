using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace GodotSimpleTools.Generators;

[Generator]
public class SingletonGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 收集所有带有SingletonAttribute的类
        var singletonClassesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => 
                    syntaxNode is ClassDeclarationSyntax,
                transform: static (generatorSyntaxContext, cancellationToken) =>
                {
                    var classDeclaration = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
                    var semanticModel = generatorSyntaxContext.SemanticModel;
                    
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);

                    // 检查是否带有SingletonAttribute
                    var singletonAttribute = classSymbol?.GetAttributes()
                        .FirstOrDefault(ad =>
                            ad.AttributeClass != null && 
                            SymbolEqualityComparer.Default.Equals(
                                ad.AttributeClass, 
                                generatorSyntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("GodotSimpleTools.SingletonAttribute")) ||
                            ad.AttributeClass?.Name == "SingletonAttribute");
                    
                    if (singletonAttribute == null)
                        return default;
                    
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
                    foreach (var arg in singletonAttribute.NamedArguments)
                    {
                        if (arg.Key == "Mode")
                        {
                            mode = (SingletonMode)(int)arg.Value.Value!;
                        }
                    }
                    
                    return (ClassSymbol: classSymbol, 
                           ClassDeclaration: classDeclaration, 
                           Mode: mode);
                })
            .Where(static x => x.ClassSymbol != null)
            .Collect();

        // 2. 为每个类生成代码
        var groupedByClassProvider = singletonClassesProvider
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) =>
            {
                var (singletonClasses, _) = pair;
                var result = new List<(INamedTypeSymbol, ClassDeclarationSyntax, SingletonMode)>();
                
                foreach (var (classSymbol, classDeclaration, mode) in singletonClasses)
                {
                    if (classSymbol == null) continue;
                    result.Add((classSymbol, classDeclaration, mode));
                }
                
                return result;
            });

        // 3. 注册代码生成
        context.RegisterSourceOutput(groupedByClassProvider,
            static (sourceProductionContext, singletonClasses) =>
            {
                foreach (var (classSymbol, _, mode) in singletonClasses)
                {
                    try
                    {
                        var sourceCode = GenerateSingletonClass(classSymbol, mode);
                        var hintName = $"{classSymbol.ToDisplayString().Replace(".", "_")}.Singleton.GST.g.cs";
        
                        sourceProductionContext.AddSource(hintName,
                            SourceText.From(sourceCode, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "GSTSG001",
                                "Singleton代码生成错误",
                                $"生成{classSymbol.Name}的Singleton代码时出错: {ex.Message}",
                                "CodeGeneration",
                                DiagnosticSeverity.Warning,
                                true),
                            Location.None));
                    }
                }
            });
    }

    private static string GenerateSingletonClass(INamedTypeSymbol classSymbol, SingletonMode mode)
    {
        var sb = new StringBuilder();
        
        // 命名空间
        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine($"namespace {classSymbol.ContainingNamespace.ToDisplayString()};");
            sb.AppendLine();
        }
        
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("// 由GodotSimpleTools的Singleton生成器生成");
        
        // 检查是否是Node派生类
        bool isNodeClass = IsOrInheritsFromNode(classSymbol);
        
        sb.AppendLine($"public partial class {classSymbol.Name}");
        sb.AppendLine("{");
        
        if (isNodeClass)
        {
            GenerateNodeSingleton(sb, classSymbol, mode);
        }
        else
        {
            GenerateNormalSingleton(sb, classSymbol, mode);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private static void GenerateNormalSingleton(StringBuilder sb, INamedTypeSymbol classSymbol, SingletonMode mode)
    {
        var className = classSymbol.Name;
        
        // 非Node类不支持AutoLoad模式，自动转为Lazy
        if (mode == SingletonMode.AutoLoad)
        {
            mode = SingletonMode.Lazy;
        }
        
        switch (mode)
        {
            case SingletonMode.Lazy:
                // 懒汉式单例
                sb.AppendLine($"    private static global::System.WeakReference<{className}>? _weakInstance;");
                sb.AppendLine($"    private static readonly object _lock = new();");
                sb.AppendLine();
            
                sb.AppendLine($"    public static {className} Instance");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine("            lock (_lock)");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (_weakInstance != null && _weakInstance.TryGetTarget(out var instance))");
                sb.AppendLine("                    return instance;");
                sb.AppendLine();
                sb.AppendLine($"                instance = new {className}();");
                sb.AppendLine($"                _weakInstance = new global::System.WeakReference<{className}>(instance);");
                sb.AppendLine("                return instance;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static void Destroy()");
                sb.AppendLine("    {");
                sb.AppendLine("        lock (_lock) _weakInstance = null;");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static bool IsAlive");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine("            lock (_lock)");
                sb.AppendLine($"                return _weakInstance?.TryGetTarget(out _) == true;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                break;
            case SingletonMode.Eager:
                // 饿汉式单例
                sb.AppendLine($"    private static readonly {className} _instance = new {className}();");
                sb.AppendLine($"    private static global::System.WeakReference<{className}> _weakInstance = new global::System.WeakReference<{className}>(_instance);");
                sb.AppendLine();
            
                sb.AppendLine($"    private {className}() {{ }}");
                sb.AppendLine();
            
                sb.AppendLine($"    public static {className} Instance");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            _weakInstance.TryGetTarget(out var instance);");
                sb.AppendLine("            return instance;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static void Destroy()");
                sb.AppendLine("    {");
                sb.AppendLine("        _weakInstance = null;");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static bool IsAlive");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            return _weakInstance?.TryGetTarget(out _) == true;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                break;
        }
    }
    
    private static void GenerateNodeSingleton(StringBuilder sb, INamedTypeSymbol classSymbol, SingletonMode mode)
    {
        var className = classSymbol.Name;

        switch (mode)
        {
            case SingletonMode.Lazy:
                sb.AppendLine($"    private static {className}? _instance;");
                sb.AppendLine($"    private static readonly object _lock = new();");
                sb.AppendLine();
            
                sb.AppendLine($"    public static {className}? Instance");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine("            lock (_lock)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (_instance == null)");
                sb.AppendLine("                {");
                sb.AppendLine("                    var root = ((global::Godot.SceneTree)global::Godot.Engine.GetMainLoop()).Root;");
                sb.AppendLine($"                    _instance = root.GetNode<{className}>(\"/root/\" + nameof({className}));");
                sb.AppendLine();
                sb.AppendLine("                    if (_instance == null)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        _instance = new {className}();");
                sb.AppendLine("                        root.AddChild(_instance);");
                sb.AppendLine($"                        global::Godot.GD.PushWarning($\"警告: {nameof(className)} 不是自动加载节点，已动态创建。\");");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("                return _instance;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static bool IsAlive => _instance != null && global::Godot.GodotObject.IsInstanceValid(_instance);");
                sb.AppendLine();
            
                // 生成_GstInitialize方法
                sb.AppendLine($"    private void _GstInitializeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance != null && _instance != this)");
                sb.AppendLine("        {");
                sb.AppendLine("            QueueFree();");
                sb.AppendLine($"            global::Godot.GD.PushError($\"存在多个{nameof(className)}实例！\");");
                sb.AppendLine("            return;");
                sb.AppendLine("        }");
                sb.AppendLine("        _instance = this;");
                sb.AppendLine("    }");
                sb.AppendLine();
        
                sb.AppendLine($"    private void _GstDisposeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance == this)");
                sb.AppendLine("            _instance = null;");
                sb.AppendLine("    }");
                break;
            case SingletonMode.Eager:
                sb.AppendLine($"    private static {className}? _instance;");
                sb.AppendLine();
            
                sb.AppendLine($"    public static {className}? Instance");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine("            if (_instance == null)");
                sb.AppendLine("            {");
                sb.AppendLine("                var root = ((global::Godot.SceneTree)global::Godot.Engine.GetMainLoop()).Root;");
                sb.AppendLine($"                _instance = root.GetNodeOrNull<{className}>(\"/root/\" + nameof({className}));");
                sb.AppendLine("            }");
                sb.AppendLine("            return _instance;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static bool IsAlive => _instance != null && global::Godot.GodotObject.IsInstanceValid(_instance);");
                sb.AppendLine();
            
                // 生成_GstInitialize方法
                sb.AppendLine($"    private void _GstInitializeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance != null && _instance != this)");
                sb.AppendLine("        {");
                sb.AppendLine("            QueueFree();");
                sb.AppendLine($"            throw new global::System.InvalidOperationException($\"{{nameof({className})}} 只能有一个实例！\");");
                sb.AppendLine("        }");
                sb.AppendLine("        _instance = this;");
                sb.AppendLine("    }");
                sb.AppendLine();
        
                sb.AppendLine($"    private void _GstDisposeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance == this)");
                sb.AppendLine("            _instance = null;");
                sb.AppendLine("    }");
                break;
            case SingletonMode.AutoLoad:
                sb.AppendLine($"    private static {className}? _instance;");
                sb.AppendLine();
            
                sb.AppendLine($"    public static {className}? Instance");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine("            if (_instance == null)");
                sb.AppendLine("            {");
                sb.AppendLine("                var tree = (global::Godot.SceneTree)global::Godot.Engine.GetMainLoop();");
                sb.AppendLine($"                _instance = tree.Root.GetNodeOrNull<{className}>(\"/root/{className}\");");
                sb.AppendLine();
                sb.AppendLine("                if (_instance == null)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    global::Godot.GD.PushError(\"{className}未在AutoLoad中配置！\");");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            return _instance;");
                sb.AppendLine("    }");
                sb.AppendLine("    }");
                sb.AppendLine();
            
                sb.AppendLine($"    public static bool IsValid => _instance != null && global::Godot.GodotObject.IsInstanceValid(_instance);");
                sb.AppendLine();
            
                // 生成_GstInitialize方法
                sb.AppendLine($"    private void _GstInitializeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance != null && _instance != this)");
                sb.AppendLine("        {");
                sb.AppendLine("            QueueFree();");
                sb.AppendLine($"            global::Godot.GD.PushError($\"检测到重复的{className}实例。已移除。\");");
                sb.AppendLine("            return;");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        _instance = this;");
                sb.AppendLine($"        global::Godot.GD.Print(\"{className}已初始化\");");
                sb.AppendLine("    }");
                sb.AppendLine();
        
                sb.AppendLine($"    private void _GstDisposeSingleton()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_instance == this)");
                sb.AppendLine("        {");
                sb.AppendLine("            _instance = null;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                break;
        }
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
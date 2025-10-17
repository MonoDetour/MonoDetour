using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MonoDetour.HookGen.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HookHelperAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "MonoDetour.HookGen";

    public static readonly DiagnosticDescriptor ProjectDoesNotReferenceRuntimeDetour = new(
        "HookGen0001",
        "Assembly does not reference MonoMod.RuntimeDetour",
        "Assembly '{0}' does not reference MonoMod.RuntimeDetour, generated helpers will not compile",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: ["CompilationEnd"]
    );

    public static readonly DiagnosticDescriptor ProjectDoesNotReferenceMonoDetour = new(
        "HookGen0002",
        "Assembly does not reference MonoDetour",
        "Assembly '{0}' does not reference MonoDetour, generated helpers will not compile",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: ["CompilationEnd"]
    );

    public static readonly DiagnosticDescriptor ReferencedTypeIsInThisAssembly = new(
        "HookGen0003",
        "Referenced type is defined in this assembly",
        "Type '{0}' is declared in this assembly. Helpers will not be generated for it.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetAssemblyIsNotPublicized = new(
        "HookGen0004",
        "Referenced type is in an assembly that is not publicized",
        "The referenced assembly '{0}' does not appear publicized. Set Publicize=\"true\" metadata on the reference item, "
            + "or add the <Publicize Include=\"{0}\"/> item to your csproj.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CannotPatchNonClassOrStruct = new(
        "HookGen0005",
        "Cannot create hook helpers for type which is not a class or struct",
        "Cannot create hook helpers for '{0}' because it is not a class or struct type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CannotPatchGenericTypes = new(
        "HookGen0006",
        "Cannot create hook helpers for generic type because generic types cannot be patched",
        "Cannot create hook helpers for '{0}' because it is generic, and generic types cannot be patched",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor HookMustBeStatic = new(
        "HookGen0007",
        "Hook method must be marked static",
        "Hook method '{0}' must be marked static",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor HookMustNotBeLambda = new(
        "HookGen0008",
        "Hook method must must not be a lambda",
        "Hook method must not be a lambda, as lambda expressions can never be truly static",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create<DiagnosticDescriptor>(
            ProjectDoesNotReferenceRuntimeDetour,
            ProjectDoesNotReferenceMonoDetour,
            ReferencedTypeIsInThisAssembly,
            TargetAssemblyIsNotPublicized,
            CannotPatchNonClassOrStruct,
            CannotPatchGenericTypes,
            HookMustBeStatic,
            HookMustNotBeLambda
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            var compilation = context.Compilation;

            var attributeType = compilation.GetTypeByMetadataName(
                HookHelperGenerator.GenHelperForTypeAttributeFqn
            );
            if (attributeType is null)
            {
                // somehow the generator isn't running, so we don't have anything to analyze
                return;
            }

            var assembliesToReport = new ConcurrentDictionary<IAssemblySymbol, int>(
                SymbolEqualityComparer.Default
            );

            context.RegisterCompilationEndAction(context =>
            {
                if (assembliesToReport.IsEmpty)
                {
                    // no types were referenced, we shouldn't report this analyzer
                    return;
                }

                var refAssNames = context.Compilation.ReferencedAssemblyNames;
                if (!refAssNames.Any(id => id.Name == "MonoMod.RuntimeDetour"))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            ProjectDoesNotReferenceRuntimeDetour,
                            null,
                            context.Compilation.AssemblyName
                        )
                    );
                }

                if (!refAssNames.Any(id => id.Name == "com.github.MonoDetour"))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            ProjectDoesNotReferenceMonoDetour,
                            null,
                            context.Compilation.AssemblyName
                        )
                    );
                }
            });

            context.RegisterOperationAction(
                context =>
                {
                    var attribute = (IAttributeOperation)context.Operation;

                    if (attribute.Operation is not IObjectCreationOperation creationOp)
                    {
                        // the attribute is invalid, don't touch it
                        return;
                    }

                    if (creationOp.Constructor is not { } ctor)
                    {
                        // no constructor? invalid attribute maybe?
                        return;
                    }

                    var attrType = ctor.ContainingType;
                    if (!SymbolEqualityComparer.Default.Equals(attributeType, attrType))
                    {
                        // the attribute is not the one we care about
                        return;
                    }

                    // now lets try to process the arguments

                    if (creationOp.Arguments is not [var argOp])
                    {
                        // no argument, bad attribute
                        return;
                    }

                    if (argOp.Value is not ITypeOfOperation typeofOp)
                    {
                        // not a typeof, invalid
                        return;
                    }

                    var targetType = typeofOp.TypeOperand;
                    var targetAssembly = targetType.ContainingAssembly;

                    if (SymbolEqualityComparer.Default.Equals(targetAssembly, compilation.Assembly))
                    {
                        // type is declared in this compilation
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                ReferencedTypeIsInThisAssembly,
                                typeofOp.Syntax.GetLocation(),
                                targetType
                            )
                        );
                        // we shouldn't report any other diagnostics for this attribute, break out
                        return;
                    }

                    // queue the assemblies to be checked for publicisation
                    if (targetAssembly is not null && assembliesToReport.TryAdd(targetAssembly, 0))
                    {
                        // we are the first to get to this assembly, analyzer it

                        // look for BepInEx.AssemblyPublicizer's marker attribute in the assembly
                        var markerAttribute = targetAssembly.GetTypeByMetadataName(
                            "BepInEx.AssemblyPublicizer.OriginalAttributesAttribute"
                        );
                        if (markerAttribute is null)
                        {
                            // the attribute is not present, this assembly does not look publicized
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    TargetAssemblyIsNotPublicized,
                                    typeofOp.Syntax.GetLocation(),
                                    targetAssembly.Identity.Name
                                )
                            );
                        }
                    }

                    if (targetType is not INamedTypeSymbol namedType)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                CannotPatchNonClassOrStruct,
                                typeofOp.Syntax.GetLocation(),
                                targetType
                            )
                        );
                    }
                    else
                    {
                        if (namedType.IsGenericType)
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    CannotPatchGenericTypes,
                                    typeofOp.Syntax.GetLocation(),
                                    targetType
                                )
                            );
                        }
                    }
                },
                OperationKind.Attribute
            );
        });

        context.RegisterSyntaxNodeAction(
            AnalyzeHookRegistrarDelegateArgIsStatic,
            SyntaxKind.InvocationExpression
        );
    }

    private void AnalyzeHookRegistrarDelegateArgIsStatic(SyntaxNodeAnalysisContext context)
    {
        var invocationExpression = (InvocationExpressionSyntax)context.Node;

        var argumentList = invocationExpression.ArgumentList;
        if (argumentList.Arguments.Count == 0)
            return;

        // Check if we are accessing a member
        if (invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!methodName.Contains("Prefix") && methodName.Contains("Postfix"))
            return;

        // Check if the hook registrar's delegate type parameter
        // is defined in the "Md" namespace
        var hookRegistrar = context.SemanticModel.GetSymbolInfo(memberAccess);
        if (hookRegistrar.Symbol is not IMethodSymbol hookRegistrarMethod)
            return;

        var hookParam = hookRegistrarMethod.Parameters.FirstOrDefault();
        if (hookParam is null)
            return;

        var namespaceSymbol = hookParam.Type.ContainingNamespace;

        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

        globalOptions.TryGetValue(
            "build_property.MonoDetourHookGenNamespace",
            out var hookGenNamespace
        );

        if (string.IsNullOrEmpty(hookGenNamespace))
        {
            hookGenNamespace = "Md";
        }

        while (namespaceSymbol is not null)
        {
            var containingNamespace = namespaceSymbol.ContainingNamespace;
            if (containingNamespace?.IsGlobalNamespace == true)
            {
                if (namespaceSymbol.Name == hookGenNamespace)
                    break;
            }
            namespaceSymbol = containingNamespace;
        }

        // The root namespace wasn't $(hookGenNamespace), not our problem
        if (namespaceSymbol is null)
            return;

        var delegateArgument = argumentList.Arguments[0].Expression;

        // If the delegate argument is a lambda expression, check it
        if (delegateArgument is LambdaExpressionSyntax)
        {
            var diagnostic = Diagnostic.Create(HookMustNotBeLambda, delegateArgument.GetLocation());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Get the symbol of the delegate argument
        var symbolInfo = context.SemanticModel.GetSymbolInfo(delegateArgument);

        // If it's a method symbol, check if it's static
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        if (!methodSymbol.IsStatic)
        {
            var diagnostic = Diagnostic.Create(
                HookMustBeStatic,
                delegateArgument.GetLocation(),
                symbolInfo.Symbol.MetadataName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}

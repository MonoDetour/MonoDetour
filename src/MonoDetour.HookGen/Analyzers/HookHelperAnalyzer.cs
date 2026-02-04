using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using MonoMod.SourceGen.Internal.Extensions;

namespace MonoDetour.HookGen.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HookHelperAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "MonoDetour.HookGen";
    private const string HookInitializeAttributeFqn =
        "MonoDetour.MonoDetourHookInitializeAttribute";

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

    public static readonly DiagnosticDescriptor MissingHookInitializeAttribute = new(
        "HookGen0009",
        $"Type marked with '{HookHelperGenerator.GenHelperForTypeAttributeFqn}'"
            + $" does not have a static method marked with '{HookInitializeAttributeFqn}'",
        $"The type '{{0}}' marked with '{HookHelperGenerator.GenHelperForTypeAttributeFqn}'"
            + $" does not have a static method marked with '{HookInitializeAttributeFqn}'"
            + " and as a result MonoDetourManager.InvokeHookInitializers will not invoke"
            + " any hook initializers for the type at runtime."
            + " If this is intended, make this an assembly attribute instead: [assembly: {1}].",
        Category,
        DiagnosticSeverity.Warning,
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
            HookMustNotBeLambda,
            MissingHookInitializeAttribute
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            var compilation = context.Compilation;

            var hookTargetsAttributeType = compilation.GetTypeByMetadataName(
                HookHelperGenerator.GenHelperForTypeAttributeFqn
            );
            if (hookTargetsAttributeType is null)
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
                    if (!SymbolEqualityComparer.Default.Equals(hookTargetsAttributeType, attrType))
                    {
                        // the attribute is not the one we care about
                        return;
                    }

                    EnsureHookTargetsTypeHasHookInitializerAttribute(context, attribute);

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

                    // queue the assemblies to be checked for publicization
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
    }

    private static void EnsureHookTargetsTypeHasHookInitializerAttribute(
        OperationAnalysisContext context,
        IAttributeOperation attribute
    )
    {
        var symbolWhichHasAttribute = context.ContainingSymbol;

        if (symbolWhichHasAttribute is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            // We deliberately don't check for stuff like if the target method is
            // static or has no parameters because I believe it's better that
            // the analyzer for the MonoDetourHookInitializeAttribute catches those.
            // Then the user has only a warning for that instead of for this too.

            if (method.HasAttributeWithFullyQualifiedMetadataName(HookInitializeAttributeFqn))
            {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MissingHookInitializeAttribute,
                attribute.Syntax.GetLocation(),
                symbolWhichHasAttribute,
                attribute.Syntax
            )
        );
    }
}

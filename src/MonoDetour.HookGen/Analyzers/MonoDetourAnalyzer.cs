using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MonoDetour.HookGen.Analyzers;

// TODO: This should be separated from HookGen.

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MonoDetourAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "MonoDetour";
    private const string HookInitializeAttributeFqn =
        "MonoDetour.MonoDetourHookInitializeAttribute";

    public static readonly DiagnosticDescriptor InvalidHookInitializeAttribute = new(
        "MonoDetour0001",
        $"Attribute '{HookInitializeAttributeFqn}' is only valid for static methods with no parameters",
        $"Attribute '{HookInitializeAttributeFqn}' is only valid for static methods with no parameters."
            + " {0}.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create<DiagnosticDescriptor>(InvalidHookInitializeAttribute);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            context.RegisterOperationAction(
                EnsureMethodWithHookInitializeAttributeIsStaticWithNoParams,
                OperationKind.Attribute
            );
        });
    }

    static void EnsureMethodWithHookInitializeAttributeIsStaticWithNoParams(
        OperationAnalysisContext context
    )
    {
        var hookInitializeAttributeType = context.Compilation.GetTypeByMetadataName(
            HookInitializeAttributeFqn
        );
        if (hookInitializeAttributeType is null)
        {
            // there are two types with the same name in the same namespace
            return;
        }

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

        if (creationOp.Arguments is not [])
        {
            return;
        }

        var attrType = ctor.ContainingType;
        if (!SymbolEqualityComparer.Default.Equals(hookInitializeAttributeType, attrType))
        {
            // the attribute is not the one we care about
            return;
        }

        var symbolWhichHasAttribute = context.ContainingSymbol;

        if (symbolWhichHasAttribute is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var paramsCount = methodSymbol.Parameters.Length;

        if (!methodSymbol.IsStatic)
        {
            var target = methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(
                Diagnostic.Create(
                    InvalidHookInitializeAttribute,
                    creationOp.Syntax.GetLocation(),
                    $"The target method '{target}' is not static"
                        + (paramsCount > 0 ? " and has parameters" : null)
                )
            );
            return;
        }

        if (paramsCount > 0)
        {
            var target = methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(
                Diagnostic.Create(
                    InvalidHookInitializeAttribute,
                    creationOp.Syntax.GetLocation(),
                    $"The target method '{target}' has parameters"
                )
            );
            return;
        }
    }
}

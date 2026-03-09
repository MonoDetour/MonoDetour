using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonoDetour.Analyzers;

[
    ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MonoDetourHookClassRefactor)),
    Shared
]
public class MonoDetourHookClassRefactor : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false)!;

        try
        {
            if (root is null)
                return;

            var node = root.FindNode(context.Span);

            var shouldNotOfferRefactoring = root.DescendantNodes()
                .Any(x => x is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

            if (shouldNotOfferRefactoring)
            {
                // RegisterFailure(context, root, "has class declaration");
                return;
            }

            var identifierSyntax =
                node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault()
                ?? node.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();

            if (identifierSyntax is null)
                return;

            var semanticModel = await context.Document.GetSemanticModelAsync(
                context.CancellationToken
            );

            if (semanticModel is null)
                return;

            var typeInfo = semanticModel.GetTypeInfo(identifierSyntax, context.CancellationToken);

            string? typeName = typeInfo.Type?.ToDisplayString(
                new SymbolDisplayFormat(
                    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes
                )
            );

            string? typeNameAndNamespaces = typeInfo.Type?.ToDisplayString(
                new SymbolDisplayFormat(
                    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
                )
            );

            if (typeName is null || typeNameAndNamespaces is null)
                return;

            var codeAction = CodeAction.Create(
                $"Complete MonoDetourTargets Hook class for type '{typeName}'",
                ct => AddMonoDetourHookAsync(context, typeName, typeNameAndNamespaces, ct),
                nameof(MonoDetourHookClassRefactor)
            );

            context.RegisterRefactoring(codeAction);
        }
        catch
        {
            // RegisterFailure(context, root!, ex.Message);
            throw;
        }
    }

    private static async Task<Document> AddMonoDetourHookAsync(
        CodeRefactoringContext context,
        string typeName,
        string typeNameAndNamespaces,
        CancellationToken ct
    )
    {
        var document = context.Document;
        var typeNameParts = typeName.Split('.');
        var partsAndNamespaces = typeNameAndNamespaces.Split('.');
        var namespacesLength = partsAndNamespaces.Length - typeNameParts.Length;
        string? usingTargetType = null;
        if (namespacesLength != 0)
        {
            // Namespace only
            usingTargetType = string.Join(".", partsAndNamespaces.Take(namespacesLength));
        }
        // Namespace + innermost type
        var usingTargetHookGen = string.Join(".", partsAndNamespaces.Take(namespacesLength + 1));

        var hookType = string.Join(".", partsAndNamespaces.Skip(namespacesLength));
        var hookTypeSafe = string.Concat(hookType.Where(x => x is not '.'));

        StringBuilder sb = new();

        var project = document.Project;
        var projectDir = Path.GetDirectoryName(project.FilePath);
        var docDir = Path.GetDirectoryName(document.FilePath);

        string inferredNamespace;

        if (projectDir == docDir)
        {
            inferredNamespace = project.Name;
        }
        else
        {
            var relativePath = GetRelativePath(projectDir, docDir)
                .Replace(Path.DirectorySeparatorChar, '.');

            inferredNamespace = $"{project.DefaultNamespace}.{relativePath}";
        }

        var newHook =
            $@"

[MonoDetourTargets(typeof({hookType}))]
static class {hookTypeSafe}Hooks
{{
    [MonoDetourHookInitialize]
    static void Init()
    {{

    }}
}}
";

        var globalOptions = context
            .Document
            .Project
            .AnalyzerOptions
            .AnalyzerConfigOptionsProvider
            .GlobalOptions;

        globalOptions.TryGetValue(
            "build_property.MonoDetourHookGenNamespace",
            out var hookGenNamespace
        );

        if (string.IsNullOrEmpty(hookGenNamespace))
        {
            hookGenNamespace = "Md";
        }

        // TODO: Maybe figure out how to do this with the DocumentEditor API.

        var newRoot = SyntaxFactory.CompilationUnit().NormalizeWhitespace();
        var mdUsingSyntax = SyntaxFactory.ParseName(hookGenNamespace + '.' + usingTargetHookGen);

        newRoot = newRoot
            .AddUsings(SyntaxFactory.UsingDirective(mdUsingSyntax))
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("MonoDetour")))
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("MonoDetour.HookGen")));

        if (usingTargetType is { })
        {
            newRoot = newRoot.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingTargetType))
            );
        }

        newRoot = WrapInNamespace(newRoot, inferredNamespace);

        var member = SyntaxFactory.ParseMemberDeclaration(newHook);

        if (member is not ClassDeclarationSyntax newMember)
            throw new Exception("member was not ClassDeclarationSyntax");

        newRoot = newRoot.AddMembers(newMember);

        return document.WithSyntaxRoot(newRoot);
    }

    // For testing purposes.
    private void RegisterFailure(CodeRefactoringContext context, SyntaxNode root, string message)
    {
        var action = CodeAction.Create(
            "Failure: " + message,
            cancellationToken =>
            {
                return Task.FromResult(context.Document.WithSyntaxRoot(root));
            }
        );

        context.RegisterRefactoring(action);
    }

    public static CompilationUnitSyntax WrapInNamespace(
        CompilationUnitSyntax root,
        string namespaceName
    )
    {
        var topLevelMembers = root.Members;
        var namespaceDecl = SyntaxFactory
            .FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .WithMembers(topLevelMembers)
            .NormalizeWhitespace();

        return root.WithMembers(
            SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDecl)
        );
    }

    public static string GetRelativePath(string basePath, string targetPath)
    {
        var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
        var targetUri = new Uri(targetPath);

        var relativeUri = baseUri.MakeRelativeUri(targetUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            return path + Path.DirectorySeparatorChar;
        }
        return path;
    }
}

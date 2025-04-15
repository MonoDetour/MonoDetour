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
                throw new Exception("root is null");

            var node = root.FindNode(context.Span);

            var shouldOfferRefactoring = !root.DescendantNodes()
                .Any(x => x is ClassDeclarationSyntax);

            if (!shouldOfferRefactoring)
            {
                // RegisterFailure(context, root, "has class declaration");
                return;
            }

            var typeReferences = root.DescendantNodes().OfType<IdentifierNameSyntax>().ToList();

            if (typeReferences.Count == 0)
                return;

            var typeReference = typeReferences[^1]
                .AncestorsAndSelf()
                .OfType<MemberAccessExpressionSyntax>()
                .FirstOrDefault();

            string typeName;
            if (typeReference is null)
            {
                typeName = node.ToString().Trim();
            }
            else
            {
                typeName = typeReference.ToString();
            }

            var codeAction = CodeAction.Create(
                $"Complete MonoDetourTargets Hook class for type '{typeName}'",
                ct => Task.FromResult(AddMonoDetourHookAsync(context, typeName, ct)),
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

    private Document AddMonoDetourHookAsync(
        CodeRefactoringContext context,
        string typeName,
        CancellationToken ct
    )
    {
        var document = context.Document;
        var parts = typeName.Split('.');

        var hookType = parts[^1].Trim();

        StringBuilder sb = new();

        // I do realize that doing this is stupid but it works
        // and I don't care about refactoring it.
        foreach (var part in parts)
        {
            if (part != hookType)
            {
                sb.Append(part);
                sb.Append('.');
            }
            else
            {
                if (sb.Length > 0)
                    sb.Length--;
                break;
            }
        }

        string usingTargetType = sb.ToString();

        var project = document.Project;
        var projectDir = Path.GetDirectoryName(project.FilePath);
        var docDir = Path.GetDirectoryName(document.FilePath);

        var relativePath = GetRelativePath(projectDir, docDir)
            .Replace(Path.DirectorySeparatorChar, '.');

        var inferredNamespace = $"{project.Name}.{relativePath}";

        var newHook =
            $@"

[MonoDetourTargets(typeof({hookType}))]
static class {hookType}Hooks
{{
    [MonoDetourHookInit]
    static void Init()
    {{

    }}
}}
";
        var newRoot = SyntaxFactory.CompilationUnit().NormalizeWhitespace();

        newRoot = newRoot.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("MonoDetour"))
        );

        if (usingTargetType != "")
        {
            newRoot = newRoot.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingTargetType))
            );
        }

        newRoot = newRoot.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("On." + typeName))
        );

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

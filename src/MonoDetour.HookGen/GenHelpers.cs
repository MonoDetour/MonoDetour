﻿using System;
using Microsoft.CodeAnalysis;
using MonoMod.SourceGen.Internal.Extensions;
using MonoMod.SourceGen.Internal.Helpers;

namespace MonoMod.SourceGen.Internal
{
    internal sealed record TypeRef(
        string MdName,
        string FqName,
        string Name,
        string Refness,
        string? ParamName,
        string? AssemblyIdentityName
    )
    {
        public TypeRef WithRefness(string refness = "")
        {
            if (Refness != refness)
            {
                return this with { Refness = refness };
            }
            else
            {
                return this;
            }
        }
    }

    internal sealed record TypeContext(
        string? Namespace,
        TypeRef InnermostType,
        string FullContextName,
        EquatableArray<string> ContainingTypeDecls
    )
    {
        public void AppendEnterContext(CodeBuilder builder, string additionalModifiers = "")
        {
            if (Namespace is not null && !Namespace.Equals("<global namespace>"))
            {
                builder.Write("namespace ").WriteLine(Namespace).OpenBlock();
            }

            var length = ContainingTypeDecls.AsImmutableArray().Length - 1;
            _ = builder.Write("namespace ").WriteLine(ContainingTypeDecls[length]).OpenBlock();

            for (var i = length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(additionalModifiers))
                {
                    _ = builder.Write(additionalModifiers).Write(' ');
                }
                _ = builder
                    .Write("internal static partial class ")
                    .WriteLine(ContainingTypeDecls[i])
                    .OpenBlock();
            }
        }

        public void AppendExitContext(CodeBuilder builder)
        {
            for (var i = 0; i < ContainingTypeDecls.AsImmutableArray().Length; i++)
            {
                _ = builder.CloseBlock();
            }
            if (Namespace is not null && !Namespace.Equals("<global namespace>"))
            {
                _ = builder.CloseBlock();
            }
        }
    }

    internal static class GenHelpers
    {
        public static TypeRef CreateRef(
            ITypeSymbol symbol,
            string refness = "",
            string? paramName = null
        )
        {
            return new(
                symbol.GetFullyQualifiedMetadataName(),
                refness + symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                symbol.Name,
                refness,
                paramName,
                symbol.ContainingAssembly?.Identity.Name
            );
        }

        public static TypeRef CreateRef(IParameterSymbol symbol)
        {
            return CreateRef(symbol.Type, GetRefString(symbol), symbol.Name);
        }

        public static string GetRefString(IParameterSymbol param, bool isReturn = false) =>
            GetRefString(param.RefKind, isReturn);

        public static string GetRefString(RefKind refKind, bool isReturn) =>
            refKind switch
            {
                RefKind.None => "",
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => isReturn ? "ref readonly " : "in ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                _ => $"/*unknown ref kind {refKind}*/ ",
            };

        public static TypeContext CreateTypeContext(
            INamedTypeSymbol type,
            string? forceTypeKind = null,
            Func<string, string>? modifyName = null
        )
        {
            var innermostType = type;

            using var builder = ImmutableArrayBuilder<string>.Rent();
            INamedTypeSymbol? outerType = null;
            while (innermostType is not null)
            {
                outerType = innermostType;

                var isRec = innermostType.IsRecord;
                var isStruct = innermostType.IsValueType;
                var isRef = innermostType.IsReferenceType;
                var typeKind =
                    forceTypeKind
                    ?? $"{(isRec ? "record" : "")}{(isRef && !isRec ? "class" : "")} {(isStruct ? "struct" : "")}";
                var name = modifyName is not null
                    ? modifyName(innermostType.Name)
                    : innermostType.Name;
                builder.Add(name);

                innermostType = innermostType.ContainingType;
            }

            var ns = outerType?.ContainingNamespace?.ToDisplayString();

            var typeCtx = "";
            innermostType = type;
            while (innermostType is not null)
            {
                typeCtx = (modifyName?.Invoke(innermostType.Name) ?? innermostType.Name) + typeCtx;
                innermostType = innermostType.ContainingType;
                if (innermostType is not null)
                    typeCtx = "." + typeCtx;
            }
            typeCtx = ns + (ns is not null ? "." : "") + typeCtx;

            var decls = builder.ToImmutable();
            return new(ns, CreateRef(type), typeCtx, decls);
        }
    }
}

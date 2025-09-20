using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace RawAccess.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public class SourceGenerator : IIncrementalGenerator
{
    private const string BaseNamespace = "RawAccess.Generated";
    private const string GenerateRawAccessAttributeName = "GenerateRawAccessAttribute";

    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGlobalNameDisplayFormat =
        new
        (
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

    private static readonly SymbolDisplayFormat FullyQualifiedWithGlobalNameDisplayFormat =
        new
        (
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

    private static readonly SymbolDisplayFormat FullyQualifiedWithGlobalWithGenericsNameDisplayFormat =
        new
        (
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput
        (
            ctx =>
            {
                ctx.AddSource
                (
                    $"{GenerateRawAccessAttributeName}.g.cs",
                    $$"""
                      using System;

                      namespace {{BaseNamespace}};

                      [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
                      public class {{GenerateRawAccessAttributeName}} : Attribute { }

                      """
                );
            }
        );

        var fromSyntaxProvider =
            context.SyntaxProvider.CreateSyntaxProvider
                (
                    static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: not 0 } or StructDeclarationSyntax { AttributeLists.Count: not 0 } or RecordDeclarationSyntax { AttributeLists.Count: not 0 },
                    static (ctx, _) =>
                    {
                        var namedTypeSymbol = ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node);
                        var attribute = namedTypeSymbol!.GetAttributes().FirstOrDefault(x => x.AttributeClass!.ToDisplayString(FullyQualifiedWithGlobalNameDisplayFormat) == $"global::{BaseNamespace}.{GenerateRawAccessAttributeName}");
                        return attribute is null ? null : namedTypeSymbol;
                    }
                )
                .Where(x => x is not null);

        var fromCompilationProvider = context.CompilationProvider.Combine(fromSyntaxProvider.Collect());

        context.RegisterSourceOutput(fromCompilationProvider, static (productionContext, syntax) => Handle(productionContext, syntax.Right!));
    }

    private static void Handle(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> symbolArr)
    {
        foreach (var namedTypeSymbol in symbolArr)
        {
            var typeFullyQualifiedWithGlobalName = namedTypeSymbol.ToDisplayString(FullyQualifiedWithGlobalNameDisplayFormat);
            var typeNameFirstLowered = namedTypeSymbol.Name.Length is 1 ? namedTypeSymbol.Name.ToLower() : $"{namedTypeSymbol.Name[0]}{namedTypeSymbol.Name.Substring(1)}";

            List<string> constraints = [];

            foreach (var typeParameterSymbol in namedTypeSymbol.TypeParameters)
            {
                List<string> typeArgumentConstraints = [];

                if (typeParameterSymbol.HasReferenceTypeConstraint)
                {
                    typeArgumentConstraints.Add("class");
                }

                if (typeParameterSymbol.HasValueTypeConstraint)
                {
                    typeArgumentConstraints.Add("struct");
                }

                if (typeParameterSymbol.HasConstructorConstraint)
                {
                    typeArgumentConstraints.Add("new()");
                }

                if (typeParameterSymbol.HasNotNullConstraint)
                {
                    typeArgumentConstraints.Add("notnull");
                }

                if (typeParameterSymbol.HasUnmanagedTypeConstraint)
                {
                    typeArgumentConstraints.Add("unmanaged");
                }

                typeArgumentConstraints.AddRange
                (
                    typeParameterSymbol.ConstraintTypes
                        .Select(x => x.ToDisplayString(FullyQualifiedWithGlobalWithGenericsNameDisplayFormat))
                );

                if (typeArgumentConstraints.Count is not 0)
                {
                    constraints.Add($"{typeParameterSymbol.Name} : {string.Join(", ", typeArgumentConstraints)}");
                }
            }

            var genericsStr = namedTypeSymbol.TypeParameters.Length is 0 ? string.Empty : $"<{string.Join(", ", namedTypeSymbol.TypeParameters.Select(x => x.Name))}>";
            var constraintsStr = constraints.Count is 0 ? string.Empty : $" where {string.Join(" where ", constraints)}";

            List<string> getTypeMethodsStrList = [];

            foreach (var constructorMethodSymbol in namedTypeSymbol.Constructors)
            {
                if (constructorMethodSymbol.DeclaredAccessibility is not Accessibility.Public)
                {
                    continue;
                }
                
                getTypeMethodsStrList.Add
                (
                    $"""
                         public static {typeFullyQualifiedWithGlobalName}{genericsStr} Get{namedTypeSymbol.Name}{genericsStr}({string.Join(", ", constructorMethodSymbol.Parameters.Select(x => $"{x.Type.ToDisplayString(FullyQualifiedWithGlobalWithGenericsNameDisplayFormat)} {x.Name}"))}){constraintsStr} =>
                             new({string.Join(", ", constructorMethodSymbol.Parameters.Select(x => x.Name))});
                     """
                );
            }

            var (propertySymbols, fieldSymbols) =
                namedTypeSymbol.GetMembers()
                    .Aggregate
                    (
                        (new List<IPropertySymbol>(), new List<IFieldSymbol>()),
                        (acc, x) =>
                        {
                            switch (x)
                            {
                                case IPropertySymbol propertySymbol:
                                    {
                                        acc.Item1.Add(propertySymbol);
                                        break;
                                    }

                                case IFieldSymbol fieldSymbol:
                                    {
                                        acc.Item2.Add(fieldSymbol);
                                        break;
                                    }
                            }

                            return acc;
                        }
                    );

            foreach (var propertySymbol in propertySymbols)
            {
                var propertyFullyQualifiedWithGlobalName = propertySymbol.Type.ToDisplayString(FullyQualifiedWithGlobalWithGenericsNameDisplayFormat);

                if (propertySymbol.GetMethod is { DeclaredAccessibility: Accessibility.Public })
                {
                    getTypeMethodsStrList.Add
                    (
                        $"""
                             public static {propertyFullyQualifiedWithGlobalName} Get{propertySymbol.Name}{genericsStr}({typeFullyQualifiedWithGlobalName}{genericsStr} {typeNameFirstLowered}){constraintsStr} =>
                                 {typeNameFirstLowered}.{propertySymbol.Name};
                         """
                    );
                }

                if (propertySymbol.SetMethod is { DeclaredAccessibility: Accessibility.Public })
                {
                    var propertyNameFirstLowered = propertySymbol.Name.Length is 1 ? propertySymbol.Name.ToLower() : $"{propertySymbol.Name[0].ToString().ToLower()}{propertySymbol.Name.Substring(1)}";

                    getTypeMethodsStrList.Add
                    (
                        namedTypeSymbol is { TypeKind: TypeKind.Class, IsRecord: false }
                            ? $$"""
                                    public static {{typeFullyQualifiedWithGlobalName}}{{genericsStr}} With{{propertySymbol.Name}}{{genericsStr}}({{typeFullyQualifiedWithGlobalName}}{{genericsStr}} {{typeNameFirstLowered}}, {{propertyFullyQualifiedWithGlobalName}} {{propertyNameFirstLowered}}){{constraintsStr}}
                                    {
                                        {{typeNameFirstLowered}}.{{propertySymbol.Name}} = {{propertyNameFirstLowered}};
                                        return {{typeNameFirstLowered}};
                                    }
                                """
                            : $$"""
                                    public static {{typeFullyQualifiedWithGlobalName}}{{genericsStr}} With{{propertySymbol.Name}}{{genericsStr}}({{typeFullyQualifiedWithGlobalName}}{{genericsStr}} {{typeNameFirstLowered}}, {{propertyFullyQualifiedWithGlobalName}} {{propertyNameFirstLowered}}){{constraintsStr}} =>
                                        {{typeNameFirstLowered}} with { {{propertySymbol.Name}} = {{propertyNameFirstLowered}} };
                                """
                    );
                }
            }

            foreach (var fieldSymbol in fieldSymbols)
            {
                if (fieldSymbol.DeclaredAccessibility is not Accessibility.Public)
                {
                    continue;
                }
                
                var fieldFullyQualifiedWithGlobalName = fieldSymbol.Type.ToDisplayString(FullyQualifiedWithGlobalWithGenericsNameDisplayFormat);

                getTypeMethodsStrList.Add
                (
                    $"""
                         public static {fieldFullyQualifiedWithGlobalName} Get{fieldSymbol.Name}{genericsStr}({typeFullyQualifiedWithGlobalName}{genericsStr} {typeNameFirstLowered}){constraintsStr} =>
                             {typeNameFirstLowered}.{fieldSymbol.Name};
                     """
                );

                var propertyNameFirstLowered = fieldSymbol.Name.Length is 1 ? fieldSymbol.Name.ToLower() : $"{fieldSymbol.Name[0].ToString().ToLower()}{fieldSymbol.Name.Substring(1)}";

                getTypeMethodsStrList.Add
                (
                    namedTypeSymbol is { TypeKind: TypeKind.Class, IsRecord: false }
                        ? $$"""
                                public static {{typeFullyQualifiedWithGlobalName}}{{genericsStr}} With{{fieldSymbol.Name}}{{genericsStr}}({{typeFullyQualifiedWithGlobalName}}{{genericsStr}} {{typeNameFirstLowered}}, {{fieldFullyQualifiedWithGlobalName}} {{propertyNameFirstLowered}}){{constraintsStr}}
                                {
                                    {{typeNameFirstLowered}}.{{fieldSymbol.Name}} = {{propertyNameFirstLowered}};
                                    return {{typeNameFirstLowered}};
                                }
                            """
                        : $$"""
                                public static {{typeFullyQualifiedWithGlobalName}}{{genericsStr}} With{{fieldSymbol.Name}}{{genericsStr}}({{typeFullyQualifiedWithGlobalName}}{{genericsStr}} {{typeNameFirstLowered}}, {{fieldFullyQualifiedWithGlobalName}} {{propertyNameFirstLowered}}){{constraintsStr}} =>
                                    {{typeNameFirstLowered}} with { {{fieldSymbol.Name}} = {{propertyNameFirstLowered}} };
                            """
                );
            }

            if (getTypeMethodsStrList.Count is 0)
            {
                continue;
            }

            var namespacesStr =
                namedTypeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? BaseNamespace
                    : $"{BaseNamespace}.{namedTypeSymbol.ContainingNamespace.ToDisplayString(FullyQualifiedWithoutGlobalNameDisplayFormat)}";

            context.AddSource
            (
                $"{namedTypeSymbol.Name}RawAccess.g.cs",
                $$"""
                  namespace {{namespacesStr}};
                  
                  public static class {{namedTypeSymbol.Name}}RawAccess
                  {
                  {{string.Join("\n\n", getTypeMethodsStrList)}}
                  }
                  
                  """
            );
        }
    }
}

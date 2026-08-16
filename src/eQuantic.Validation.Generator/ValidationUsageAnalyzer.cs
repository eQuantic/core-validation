using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace eQuantic.Validation.Generator;

/// <summary>
/// Compile-time detection of validator usage mistakes that would otherwise only surface as
/// runtime exceptions: invalid RuleFor expressions, configurators before any rule, and
/// synchronous validation of validators with unconditional asynchronous rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidationUsageAnalyzer : DiagnosticAnalyzer
{
    private const string ValidatorNamespace = "eQuantic.Validation";

    private static readonly DiagnosticDescriptor InvalidRuleExpression = new(
        id: "VALGEN005",
        title: "RuleFor requires a simple member expression",
        messageFormat: "RuleFor/RuleForEach accepts a direct member expression such as 'x => x.Email' or 'x => x.Address.City'; this lambda would throw ArgumentException at runtime",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConfiguratorBeforeRule = new(
        id: "VALGEN006",
        title: "Configurator called before any rule",
        messageFormat: "'{0}' configures the preceding rule; add a rule (NotNull, Email, Must, ...) before calling it, otherwise the validator throws InvalidOperationException when constructed",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SyncValidateWithAsyncRules = new(
        id: "VALGEN007",
        title: "Synchronous Validate on a validator with asynchronous rules",
        messageFormat: "'{0}' declares unconditional asynchronous rules; calling Validate throws AsyncValidationRequiredException at runtime — use ValidateAsync",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(InvalidRuleExpression, ConfiguratorBeforeRule, SyncValidateWithAsyncRules);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var asyncRuleCache = new ConcurrentDictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, asyncRuleCache),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<INamedTypeSymbol, bool> asyncRuleCache)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null,
        };

        switch (methodName)
        {
            case "RuleFor" or "RuleForEach":
                AnalyzeRuleExpression(context, invocation);
                break;

            case "WithMessage" or "WithCode" or "WithSeverity":
                AnalyzeConfiguratorPlacement(context, invocation, methodName);
                break;

            case "Validate":
                AnalyzeSyncValidate(context, invocation, asyncRuleCache);
                break;
        }
    }

    private static void AnalyzeRuleExpression(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1 ||
            !IsValidationLibraryMethod(context, invocation))
        {
            return;
        }

        var lambdaBody = invocation.ArgumentList.Arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple.ExpressionBody,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ExpressionBody,
            _ => null,
        };

        if (lambdaBody is null)
        {
            return;
        }

        var current = Unwrap(lambdaBody);
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            current = Unwrap(memberAccess.Expression);
        }

        if (current is not IdentifierNameSyntax)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidRuleExpression, lambdaBody.GetLocation()));
        }
    }

    private static void AnalyzeConfiguratorPlacement(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string methodName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Expression is not InvocationExpressionSyntax receiver)
        {
            return;
        }

        var receiverName = receiver.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax receiverMember => receiverMember.Name.Identifier.ValueText,
            _ => null,
        };

        if (receiverName is not ("RuleFor" or "RuleForEach" or "RuleForModel") ||
            !IsValidationLibraryMethod(context, receiver))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConfiguratorBeforeRule, memberAccess.Name.GetLocation(), methodName));
    }

    private static void AnalyzeSyncValidate(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ConcurrentDictionary<INamedTypeSymbol, bool> asyncRuleCache)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !IsValidationLibraryMethod(context, invocation))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type
                is not INamedTypeSymbol receiverType ||
            receiverType.IsAbstract ||
            receiverType.TypeKind != TypeKind.Class ||
            !InheritsFromValidator(receiverType) ||
            receiverType.DeclaringSyntaxReferences.IsEmpty)
        {
            return;
        }

        var hasUnconditionalAsyncRules = asyncRuleCache.GetOrAdd(
            receiverType,
            static type => DeclaresUnconditionalAsyncRules(type));

        if (hasUnconditionalAsyncRules)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SyncValidateWithAsyncRules, memberAccess.Name.GetLocation(), receiverType.Name));
        }
    }

    private static bool DeclaresUnconditionalAsyncRules(INamedTypeSymbol validatorType)
    {
        foreach (var reference in validatorType.DeclaringSyntaxReferences)
        {
            foreach (var invocation in reference.GetSyntax().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = invocation.Expression switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    _ => null,
                };

                if (name is not ("MustAsync" or "MatchAsync"))
                {
                    continue;
                }

                // A ForScenarios/When call in the same statement makes the async rule conditional:
                // synchronous validation outside those scenarios remains legal.
                var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
                var isConditional = statement is not null && statement
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(static candidate => candidate.Expression is MemberAccessExpressionSyntax candidateMember &&
                        candidateMember.Name.Identifier.ValueText is "ForScenarios" or "When");

                if (!isConditional)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsValidationLibraryMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method)
        {
            return false;
        }

        for (INamespaceSymbol? current = method.ContainingType?.ContainingNamespace;
             current is { IsGlobalNamespace: false };
             current = current.ContainingNamespace)
        {
            if (current.ToDisplayString() == ValidatorNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InheritsFromValidator(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.OriginalDefinition.MetadataName == "Validator`1" &&
                current.OriginalDefinition.ContainingNamespace.ToDisplayString() == ValidatorNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            expression = postfix.Operand;
        }

        return expression;
    }
}

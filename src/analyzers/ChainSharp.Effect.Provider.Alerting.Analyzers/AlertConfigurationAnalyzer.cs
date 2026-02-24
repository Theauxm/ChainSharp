using System.Collections.Immutable;
using System.Linq;
using ChainSharp.Effect.Provider.Alerting.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ChainSharp.Effect.Provider.Alerting.Analyzers;

/// <summary>
/// Analyzer that validates AlertConfigurationBuilder.Build() is called only after
/// setting required fields (TimeWindow and MinimumFailures).
/// </summary>
/// <remarks>
/// This analyzer triggers on .Build() calls on AlertConfigurationBuilder and walks
/// backward through the fluent chain to verify that either:
/// 1. Both WithinTimeSpan() and MinimumFailures() were called, OR
/// 2. AlertOnEveryFailure() was called
///
/// Diagnostic ALERT001 is reported if neither condition is satisfied.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AlertConfigurationAnalyzer : DiagnosticAnalyzer
{
    private const string AlertConfigurationBuilderTypeName =
        "ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AlertingDiagnosticDescriptors.AlertConfigurationRequiresFields);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a Build() call
        if (!IsBuildCall(invocation))
            return;

        // Verify it's on AlertConfigurationBuilder
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (containingType != AlertConfigurationBuilderTypeName)
            return;

        // Walk backward through the fluent chain to check for required calls
        var chain = CollectFluentChain(invocation);
        var hasTimeWindow = chain.Any(
            name => name == "WithinTimeSpan" || name == "AlertOnEveryFailure"
        );
        var hasMinimumFailures = chain.Any(
            name => name == "MinimumFailures" || name == "AlertOnEveryFailure"
        );

        // Report diagnostic if either required field is missing
        if (!hasTimeWindow || !hasMinimumFailures)
        {
            var diagnostic = Diagnostic.Create(
                AlertingDiagnosticDescriptors.AlertConfigurationRequiresFields,
                invocation.GetLocation()
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Checks if an invocation is a call to a method named "Build".
    /// </summary>
    private static bool IsBuildCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => memberAccess.Name.Identifier.Text == "Build",
            IdentifierNameSyntax identifier => identifier.Identifier.Text == "Build",
            _ => false
        };
    }

    /// <summary>
    /// Collects all method names in the fluent chain leading to the current invocation.
    /// </summary>
    /// <param name="invocation">The Build() invocation to start from</param>
    /// <returns>List of method names in the chain</returns>
    /// <remarks>
    /// This walks backward through the fluent chain (from Build() to Create())
    /// and collects all method names. For example:
    ///
    /// AlertConfigurationBuilder.Create()
    ///     .WithinTimeSpan(...)
    ///     .MinimumFailures(...)
    ///     .Build()
    ///
    /// Returns: ["Build", "MinimumFailures", "WithinTimeSpan", "Create"]
    /// </remarks>
    private static System.Collections.Generic.List<string> CollectFluentChain(
        InvocationExpressionSyntax invocation
    )
    {
        var methodNames = new System.Collections.Generic.List<string>();
        var current = invocation;

        while (current != null)
        {
            // Get the method name from the current invocation
            var methodName = current.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            if (methodName != null)
                methodNames.Add(methodName);

            // Move to the previous call in the chain
            current =
                (current.Expression as MemberAccessExpressionSyntax)?.Expression
                as InvocationExpressionSyntax;
        }

        return methodNames;
    }
}

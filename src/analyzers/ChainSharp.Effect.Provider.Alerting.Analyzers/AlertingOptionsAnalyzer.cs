using System.Collections.Immutable;
using System.Linq;
using ChainSharp.Effect.Provider.Alerting.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ChainSharp.Effect.Provider.Alerting.Analyzers;

/// <summary>
/// Analyzer that validates UseAlertingEffect() is called with at least one AddAlertSender().
/// </summary>
/// <remarks>
/// This analyzer triggers on UseAlertingEffect() calls and examines the lambda parameter
/// to verify that AddAlertSender&lt;T&gt;() is called at least once.
///
/// Diagnostic ALERT002 is reported if no AddAlertSender calls are found.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AlertingOptionsAnalyzer : DiagnosticAnalyzer
{
    private const string UseAlertingEffectMethodName = "UseAlertingEffect";
    private const string AddAlertSenderMethodName = "AddAlertSender";
    private const string AlertingOptionsBuilderTypeName =
        "ChainSharp.Effect.Provider.Alerting.Models.AlertingOptionsBuilder";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AlertingDiagnosticDescriptors.UseAlertingEffectRequiresSender);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // Check if this is a UseAlertingEffect() call
        if (invocation.TargetMethod.Name != UseAlertingEffectMethodName)
            return;

        // Get the first argument (should be the configure lambda)
        if (invocation.Arguments.Length == 0)
            return;

        var firstArgument = invocation.Arguments[0];

        // Extract the lambda expression
        var lambdaOperation = firstArgument.Value switch
        {
            IDelegateCreationOperation delegateCreation => delegateCreation.Target,
            IAnonymousFunctionOperation anonymousFunction => anonymousFunction,
            _ => null
        };

        if (lambdaOperation is not IAnonymousFunctionOperation lambda)
            return;

        // Analyze the lambda body for AddAlertSender calls
        var hasAddAlertSender = ContainsAddAlertSenderCall(lambda.Body);

        if (!hasAddAlertSender)
        {
            var diagnostic = Diagnostic.Create(
                AlertingDiagnosticDescriptors.UseAlertingEffectRequiresSender,
                invocation.Syntax.GetLocation()
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Recursively searches an operation tree for AddAlertSender calls.
    /// </summary>
    /// <param name="operation">The operation to search (typically a lambda body)</param>
    /// <returns>True if AddAlertSender is called, false otherwise</returns>
    private static bool ContainsAddAlertSenderCall(IOperation? operation)
    {
        if (operation == null)
            return false;

        // Check if this operation is an AddAlertSender invocation
        if (operation is IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name == AddAlertSenderMethodName)
            {
                // Verify it's on AlertingOptionsBuilder
                var receiverType = invocation.TargetMethod.ContainingType?.ToDisplayString();
                if (receiverType == AlertingOptionsBuilderTypeName)
                    return true;
            }
        }

        // Recursively search child operations
        foreach (var child in operation.ChildOperations)
        {
            if (ContainsAddAlertSenderCall(child))
                return true;
        }

        return false;
    }
}

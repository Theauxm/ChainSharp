using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.StepEffectRunner;

public class StepEffectRunner : IStepEffectRunner
{
    private List<IStepEffectProvider> ActiveStepEffectProviders { get; init; }

    private readonly ILogger<StepEffectRunner>? _logger;

    public StepEffectRunner(
        IEnumerable<IStepEffectProviderFactory> stepEffectProviderFactories,
        ILogger<StepEffectRunner>? logger = null
    )
    {
        _logger = logger;

        ActiveStepEffectProviders = [];
        ActiveStepEffectProviders.AddRange(
            stepEffectProviderFactories.RunAll(factory => factory.Create())
        );
    }

    public async Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        await ActiveStepEffectProviders.RunAllAsync(
            provider => provider.BeforeStepExecution(effectStep, effectWorkflow, cancellationToken)
        );
    }

    public async Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        await ActiveStepEffectProviders.RunAllAsync(
            provider => provider.AfterStepExecution(effectStep, effectWorkflow, cancellationToken)
        );
    }

    public void Dispose()
    {
        var disposalExceptions = new List<Exception>();

        foreach (var provider in ActiveStepEffectProviders)
        {
            try
            {
                provider?.Dispose();
            }
            catch (Exception ex)
            {
                disposalExceptions.Add(ex);
                _logger?.LogError(
                    ex,
                    "Failed to dispose effect provider of type ({ProviderType}). Provider disposal will continue for remaining providers.",
                    provider?.GetType().Name ?? "Unknown"
                );
            }
        }

        ActiveStepEffectProviders.Clear();

        // If we had disposal exceptions, log the summary
        if (disposalExceptions.Count > 0)
        {
            _logger?.LogWarning(
                "Completed provider disposal with ({ExceptionCount}) provider(s) failing to dispose properly. "
                    + "Memory leaks may have occurred in the failed providers.",
                disposalExceptions.Count
            );
        }
        else
        {
            _logger?.LogDebug(
                "Successfully disposed all ({ProviderCount}) effect provider(s).",
                ActiveStepEffectProviders.Count
            );
        }
    }
}

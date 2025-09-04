using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ChainSharp.Effect.Step.Logging.Services.StepLoggerProvider;

public class StepLoggerProvider(
    IChainSharpEffectConfiguration configuration,
    ILogger<StepLoggerProvider> logger
) : IStepLoggerProvider
{
    public async Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        if (effectStep.Metadata is null)
            throw new WorkflowException(
                "Effect Step's Metadata should be null. Something has gone horribly wrong."
            );

        logger.Log(configuration.LogLevel, "{@StepMetadata}", effectStep.Metadata);
    }

    public async Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        if (effectStep.Metadata is null)
            throw new WorkflowException(
                "Effect Step's Metadata should be null. Something has gone horribly wrong."
            );

        effectStep.Result.Match(
            Right: resultOut =>
            {
                if (resultOut is null)
                    return;

                effectStep.Metadata.OutputJson = configuration.SerializeStepData
                    ? JsonConvert.SerializeObject(
                        resultOut,
                        configuration.NewtonsoftJsonSerializerSettings
                    )
                    : null;
            },
            Left: _ => { },
            Bottom: () => { }
        );

        logger.Log(configuration.LogLevel, "{@Metadata}", effectStep.Metadata);
    }

    public void Dispose() { }
}

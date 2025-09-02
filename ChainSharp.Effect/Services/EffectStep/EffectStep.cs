using ChainSharp.Effect.Models.StepMetadata;
using ChainSharp.Effect.Models.StepMetadata.DTOs;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using ChainSharp.Step;
using ChainSharp.Workflow;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.EffectStep;

public abstract class EffectStep<TIn, TOut> : Step<TIn, TOut>, IEffectStep<TIn, TOut>
{
    /// <summary>
    /// The core implementation method that performs the step's operation.
    /// This must be implemented by derived classes.
    /// </summary>
    /// <param name="input">The input data for this step</param>
    /// <returns>The output produced by this step</returns>
    public abstract override Task<TOut> Run(TIn input);

    private StepMetadata? Metadata { get; set; }

    public override Task<Either<Exception, TOut>> RailwayStep<TWorkflowIn, TWorkflowOut>(
        Either<Exception, TIn> previousOutput,
        Workflow<TWorkflowIn, TWorkflowOut> workflow
    )
    {
        if (workflow is not EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow)
            throw new WorkflowException(
                $"Cannot run an EffectStep ({GetType().Name}) against a non-EffectWorkflow ({workflow.GetType().Name})"
            );

        return RailwayStep(previousOutput, effectWorkflow);
    }

    public async Task<Either<Exception, TOut>> RailwayStep<TWorkflowIn, TWorkflowOut>(
        Either<Exception, TIn> previousOutput,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow
    )
    {
        Metadata = StepMetadata.Create(
            new CreateStepMetadata
            {
                Name = GetType().Name,
                ExternalId = Guid.NewGuid().ToString("N"),
                InputType = typeof(TIn),
                OutputType = typeof(TOut),
                State = previousOutput.State,
                WorkflowExternalId = effectWorkflow.ExternalId
            }
        );

        effectWorkflow.Steps.AddLast(Metadata);

        if (effectWorkflow.StepEffectRunner is not null)
            await effectWorkflow.StepEffectRunner.BeforeStepExecution(
                this,
                effectWorkflow,
                CancellationToken.None
            );

        Metadata.StartTimeUtc = DateTime.UtcNow;

        var result = await base.RailwayStep(previousOutput, effectWorkflow);

        Metadata.EndTimeUtc = DateTime.UtcNow;
        Metadata.State = result.State;

        if (effectWorkflow.StepEffectRunner is not null)
            await effectWorkflow.StepEffectRunner.AfterStepExecution(
                this,
                effectWorkflow,
                CancellationToken.None
            );

        return result;
    }
}

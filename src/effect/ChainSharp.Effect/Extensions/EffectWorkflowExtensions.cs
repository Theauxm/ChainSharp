using System.Runtime.CompilerServices;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Extensions;

internal static class EffectWorkflowExtensions
{
    /// <summary>
    /// Initializes the workflow metadata in the database and sets the initial state.
    /// </summary>
    /// <param name="metadata">The metadata for the workflow</param>
    /// <param name="effectWorkflow"></param>
    /// <returns>The created metadata object that will track this workflow execution</returns>
    /// <exception cref="WorkflowException">Thrown if the EffectRunner is not available</exception>
    internal static async Task<Unit> InitializeWorkflow<TIn, TOut>(
        this EffectWorkflow<TIn, TOut> effectWorkflow
    )
    {
        effectWorkflow.EffectRunner.AssertLoaded();

        effectWorkflow.Logger?.LogTrace(
            "Initializing ({WorkflowName})",
            effectWorkflow.WorkflowName
        );
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = effectWorkflow.WorkflowName,
                ExternalId = effectWorkflow.ExternalId,
                Input = null,
                ParentId = effectWorkflow.ParentId
            }
        );

        return await effectWorkflow.InitializeWorkflow(metadata);
    }

    /// <summary>
    /// Initializes the workflow metadata in the database and sets the initial state.
    /// </summary>
    /// <param name="effectWorkflow"></param>
    /// <param name="metadata">The metadata for the workflow</param>
    /// <returns>The created metadata object that will track this workflow execution</returns>
    /// <exception cref="WorkflowException">Thrown if the EffectRunner is not available</exception>
    internal static async Task<Unit> InitializeWorkflow<TIn, TOut>(
        this EffectWorkflow<TIn, TOut> effectWorkflow,
        Metadata metadata
    )
    {
        effectWorkflow.EffectRunner.AssertLoaded();

        if (metadata.WorkflowState != WorkflowState.Pending)
            throw new WorkflowException(
                $"Cannot start a workflow with state ({metadata.WorkflowState}), must be Pending."
            );

        await effectWorkflow.EffectRunner.Track(metadata);
        effectWorkflow.Logger?.LogTrace(
            "Initializing ({WorkflowName})",
            effectWorkflow.WorkflowName
        );
        effectWorkflow.Metadata = metadata;

        return await effectWorkflow.StartWorkflow(metadata);
    }

    internal static async Task<Unit> StartWorkflow<TIn, TOut>(
        this EffectWorkflow<TIn, TOut> effectWorkflow,
        Metadata metadata
    )
    {
        effectWorkflow.EffectRunner.AssertLoaded();
        effectWorkflow.Metadata.AssertLoaded();

        if (metadata.WorkflowState != WorkflowState.Pending)
            throw new WorkflowException(
                $"Cannot start a workflow with state ({metadata.WorkflowState}), must be Pending."
            );

        effectWorkflow.Logger?.LogTrace(
            "Setting ({WorkflowName}) to In Progress.",
            effectWorkflow.WorkflowName
        );
        effectWorkflow.Metadata.WorkflowState = WorkflowState.InProgress;

        await effectWorkflow.EffectRunner.Update(effectWorkflow.Metadata);

        return Unit.Default;
    }

    /// <summary>
    /// Updates the workflow metadata to reflect the final state of the workflow execution.
    /// </summary>
    /// <param name="effectWorkflow"></param>
    /// <param name="result">The Either containing either an Exception (Left) or the workflow result (Right)</param>
    /// <returns>A Unit value (similar to void, but functional)</returns>
    /// <exception cref="WorkflowException">Thrown if the Metadata object is not available</exception>
    /// <remarks>
    /// This method:
    /// 1. Determines if the workflow completed successfully or failed based on the Either result
    /// 2. Updates the workflow state accordingly (Completed or Failed)
    /// 3. Records the end time of the workflow
    /// 4. If the workflow failed, adds the exception details to the metadata
    ///
    /// The Railway-oriented programming pattern is used here, where the Either type
    /// represents either success (Right) or failure (Left).
    /// </remarks>
    internal static async Task<Unit> FinishWorkflow<TIn, TOut>(
        this EffectWorkflow<TIn, TOut> effectWorkflow,
        Either<Exception, TOut> result
    )
    {
        effectWorkflow.EffectRunner.AssertLoaded();
        effectWorkflow.Metadata.AssertLoaded();

        var failureReason = result.IsRight ? null : result.Swap().ValueUnsafe();

        var resultState = result.IsRight
            ? WorkflowState.Completed
            : failureReason is OperationCanceledException
                ? WorkflowState.Cancelled
                : WorkflowState.Failed;
        effectWorkflow.Logger?.LogTrace(
            "Setting ({WorkflowName}) to ({ResultState}).",
            effectWorkflow.WorkflowName,
            resultState.ToString()
        );
        effectWorkflow.Metadata.WorkflowState = resultState;
        effectWorkflow.Metadata.EndTime = DateTime.UtcNow;
        effectWorkflow.Metadata.CurrentlyRunningStep = null;
        effectWorkflow.Metadata.StepStartedAt = null;

        if (failureReason != null)
            effectWorkflow.Metadata.AddException(failureReason);

        await effectWorkflow.EffectRunner.Update(effectWorkflow.Metadata);

        return Unit.Default;
    }
}

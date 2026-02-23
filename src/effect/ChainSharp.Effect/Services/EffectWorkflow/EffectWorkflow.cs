using System.Text.Json.Serialization;
using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Models.StepMetadata;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Effect.Services.StepEffectRunner;
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.EffectWorkflow;

/// <summary>
/// Extends the base Workflow class to add database tracking and logging capabilities.
/// This class automatically records workflow execution details including inputs, outputs,
/// execution time, and error information to a persistent store.
/// </summary>
/// <typeparam name="TIn">The input type for the workflow</typeparam>
/// <typeparam name="TOut">The output type for the workflow</typeparam>
/// <remarks>
/// EffectWorkflow is the core class for adding side effects to workflows.
/// It wraps the execution of the base workflow in database operations that
/// track the workflow's lifecycle, making it possible to monitor, audit,
/// and debug workflow executions.
/// </remarks>
public abstract class EffectWorkflow<TIn, TOut> : Workflow<TIn, TOut>, IEffectWorkflow<TIn, TOut>
{
    /// <summary>
    /// Database Metadata row associated with the workflow. Contains all tracking information
    /// about this workflow execution including inputs, outputs, timing, and error details.
    /// </summary>
    /// <remarks>
    /// This property is populated during the InitializeWorkflow method and is used
    /// throughout the workflow lifecycle to record execution details.
    /// </remarks>
    [JsonIgnore]
    public Metadata? Metadata { get; internal set; }

    /// <summary>
    /// ParentId for the workflow, used to establish parent-child relationships between workflows.
    /// When a workflow is triggered by another workflow, this property contains the parent's ID.
    /// </summary>
    /// <remarks>
    /// This enables hierarchical tracking of workflow executions, allowing for complex
    /// workflow compositions to be visualized and analyzed.
    /// </remarks>
    internal long? ParentId { get; set; }

    /// <summary>
    /// The EffectRunner is responsible for managing all effect providers and persisting
    /// workflow metadata to the underlying storage systems.
    /// </summary>
    /// <remarks>
    /// This property is automatically injected by the dependency injection system
    /// when using the [Inject] attribute. The EffectRunner must be properly registered
    /// in the service collection using AddChainSharpEffects().
    /// </remarks>
    [Inject]
    [JsonIgnore]
    public IEffectRunner? EffectRunner { get; set; }

    [Inject]
    [JsonIgnore]
    public IStepEffectRunner? StepEffectRunner { get; set; }

    /// <summary>
    /// Logger specific to this workflow type, used for recording diagnostic information
    /// about the workflow execution.
    /// </summary>
    /// <remarks>
    /// Automatically injected via property injection using the [Inject] attribute.
    /// </remarks>
    [Inject]
    [JsonIgnore]
    public ILogger<EffectWorkflow<TIn, TOut>>? Logger { get; set; }

    /// <summary>
    /// The service provider used to resolve dependencies within the workflow.
    /// </summary>
    /// <remarks>
    /// Automatically injected via property injection using the [Inject] attribute.
    /// This is passed to the base workflow's Run method to enable dependency resolution.
    /// </remarks>
    [Inject]
    [JsonIgnore]
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Gets the name of the concrete workflow class that inherits from EffectWorkflow.
    /// </summary>
    /// <remarks>
    /// This is used for logging and metadata purposes to identify the specific workflow type.
    /// </remarks>
    internal string WorkflowName =>
        GetType().FullName
        ?? throw new WorkflowException($"Could not find FullName for ({GetType().Name})");

    /// <summary>
    /// Overrides the base Workflow Run method to add database tracking and logging capabilities.
    /// This method wraps the execution of the base workflow in database operations that record
    /// the workflow's lifecycle events.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>The result of the workflow execution</returns>
    /// <exception cref="WorkflowException">Thrown if required dependencies are missing or if the workflow fails</exception>
    /// <remarks>
    /// The execution flow is:
    /// 1. Validate that required dependencies (EffectRunner, ServiceProvider) are available
    /// 2. Initialize workflow metadata in the database
    /// 3. Execute the base workflow
    /// 4. Record the result (success or failure) in the database
    /// 5. Clean up resources
    ///
    /// Any exceptions thrown during execution are caught, recorded in the database,
    /// and then re-thrown to maintain the original error behavior.
    /// </remarks>
    public new virtual async Task<TOut> Run(TIn input)
    {
        EffectRunner.AssertLoaded();
        StepEffectRunner.AssertLoaded();
        ServiceProvider.AssertLoaded();

        if (Metadata == null)
            await this.InitializeWorkflow();

        Metadata.AssertLoaded();
        await EffectRunner.SaveChanges(CancellationToken.None);

        try
        {
            Logger?.LogTrace("Running Workflow: ({WorkflowName})", WorkflowName);
            Metadata.SetInputObject(input);
            var result = await base.Run(input, ServiceProvider);
            Logger?.LogTrace("({WorkflowName}) completed successfully.", WorkflowName);
            Metadata.SetOutputObject(result);

            await EffectRunner.Update(Metadata);

            await this.FinishWorkflow(result);
            await EffectRunner.SaveChanges(CancellationToken.None);

            return result;
        }
        catch (Exception e)
        {
            Logger?.LogError(
                "Caught Exception ({Type}) with Message ({Message}).",
                e.GetType(),
                e.Message
            );

            await this.FinishWorkflow(e);
            await EffectRunner.SaveChanges(CancellationToken.None);

            throw;
        }
    }

    public virtual async Task<TOut> Run(TIn input, Metadata metadata)
    {
        await this.InitializeWorkflow(metadata);
        return await Run(input);
    }

    /// <summary>
    /// Abstract method that must be implemented by concrete workflow classes.
    /// This method contains the core business logic of the workflow.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>An Either containing either an Exception (Left) or the workflow result (Right)</returns>
    /// <remarks>
    /// This method follows the Railway-oriented programming pattern, where the result
    /// is an Either type that can represent either success (Right) or failure (Left).
    ///
    /// Implementations should:
    /// 1. Perform the core business logic of the workflow
    /// 2. Return a Right value with the result on success
    /// 3. Return a Left value with an exception on failure
    ///
    /// The base Workflow class will handle the execution of this method and
    /// the EffectWorkflow class will handle tracking and logging.
    /// </remarks>
    protected abstract override Task<Either<Exception, TOut>> RunInternal(TIn input);

    public void Dispose()
    {
        // Clear large objects from metadata to prevent memory retention
        if (Metadata != null)
        {
            // Clear input/output objects which might be large
            Metadata.SetInputObject(null);
            Metadata.SetOutputObject(null);
        }

        EffectRunner?.Dispose();
        StepEffectRunner?.Dispose();
        Metadata?.Dispose();

        // Release all step inputs/outputs and services held in the workflow Memory dictionary
        Memory?.Clear();

        Logger = null;
        ServiceProvider = null;
    }
}

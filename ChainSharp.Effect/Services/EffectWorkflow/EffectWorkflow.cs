using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.EffectWorkflow;

/// <summary>
/// Adds information to the database about a given workflow being run.
/// </summary>
/// <typeparam name="TIn"></typeparam>
/// <typeparam name="TOut"></typeparam>
public abstract class EffectWorkflow<TIn, TOut> : Workflow<TIn, TOut>, IEffectWorkflow<TIn, TOut>
{
    /// <summary>
    /// Database Metadata row associated with the workflow
    /// </summary>
    public Metadata? Metadata { get; private set; }

    /// <summary>
    /// ParentId for the workflow, used in the initializer where it is passed into the metadata
    /// </summary>
    internal int? ParentId { get; set; }

    /// <summary>
    /// DataContextFactory for all connections required in the Workflow
    /// </summary>
    [Inject]
    public IEffectRunner? EffectRunner { get; set; }

    [Inject]
    public ILogger<EffectWorkflow<TIn, TOut>>? EffectLogger { get; set; }

    [Inject]
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Gets base type name, typically the name of the class inheriting the LoggedWorkflow
    /// </summary>
    private string WorkflowName => GetType().Name;

    /// <summary>
    /// Overrides base Workflow Run to also include details about the workflow itself in
    /// the database, specifically the `workflow.workflow` table.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public new virtual async Task<TOut> Run(TIn input)
    {
        if (EffectRunner == null)
        {
            EffectLogger?.LogCritical(
                "EffectRunner is null. Ensure services.AddChainSharpEffects() is being added to your Dependency Injection Container."
            );
            throw new WorkflowException(
                "EffectRunner is null. Ensure services.AddChainSharpEffects() is being added to your Dependency Injection Container."
            );
        }

        if (ServiceProvider == null)
        {
            EffectLogger?.LogCritical(
                "Could not find injected IServiceProvider. Is it being injected into the ServiceCollection?"
            );
            throw new WorkflowException(
                "Could not find injected IServiceProvider. Is it being injected into the ServiceCollection?"
            );
        }

        EffectLogger?.LogTrace($"Running Workflow: ({WorkflowName})");

        Metadata = await InitializeWorkflow(input);
        await EffectRunner.SaveChanges(CancellationToken.None);

        try
        {
            var result = await base.Run(input, ServiceProvider);
            EffectLogger?.LogTrace($"({WorkflowName}) completed successfully.");
            Metadata.OutputObject = result;

            await FinishWorkflow(result);
            await EffectRunner.SaveChanges(CancellationToken.None);

            return result;
        }
        catch (Exception e)
        {
            EffectLogger?.LogError($"Caught Exception ({e.GetType()}) with Message ({e.Message}).");

            await FinishWorkflow(e);
            await EffectRunner.SaveChanges(CancellationToken.None);

            throw;
        }
        finally
        {
            EffectRunner.Dispose();
        }
    }

    /// <summary>
    /// initializes and begins the Workflow.
    /// </summary>
    /// <param name="effectRunner"></param>
    /// <param name="workflowName"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    private async Task<Metadata> InitializeWorkflow(TIn input)
    {
        if (EffectRunner == null)
            throw new WorkflowException(
                "EffectLogger is null. Something has gone horribly wrong. Ensure services.AddChainSharpEffects() is being added to your Dependency Injection Container."
            );

        EffectLogger?.LogTrace($"Initializing ({WorkflowName})");

        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = WorkflowName,
                Input = input,
                ParentId = ParentId
            }
        );
        await EffectRunner.Track(metadata);

        EffectLogger?.LogTrace($"Setting ({WorkflowName}) to In Progress.");
        metadata.WorkflowState = WorkflowState.InProgress;

        return metadata;
    }

    /// <summary>
    /// Finishes a Workflow with either an exception or a result.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="metadata"></param>
    /// <param name="workflowName"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    private async Task<Unit> FinishWorkflow(Either<Exception, TOut> result)
    {
        if (Metadata == null)
            throw new WorkflowException(
                "Metadata object has not been set. Was Run() called initially? If so, please submit a ticket on the ChainSharp GitHub repository. Something has gone horribly wrong."
            );

        var failureReason = result.IsRight ? null : result.Swap().ValueUnsafe();

        var resultState = result.IsRight ? WorkflowState.Completed : WorkflowState.Failed;
        EffectLogger?.LogTrace($"Setting ({WorkflowName}) to ({resultState.ToString()}).");
        Metadata.WorkflowState = resultState;
        Metadata.EndTime = DateTime.UtcNow;

        if (failureReason != null)
            Metadata.AddException(failureReason);

        return Unit.Default;
    }

    protected abstract override Task<Either<Exception, TOut>> RunInternal(TIn input);
}

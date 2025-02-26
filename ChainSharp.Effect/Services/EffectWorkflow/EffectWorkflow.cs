using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectFactory;
using ChainSharp.Effect.Services.EffectLogger;
using ChainSharp.Workflow;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

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
    public Metadata Metadata { get; private set; }

    /// <summary>
    /// DataContextFactory for all connections required in the Workflow
    /// </summary>
    [Inject]
    public IEnumerable<IEffectFactory> EffectFactories { get; set; }

    [Inject]
    public IEnumerable<IEffectLogger> Loggers { get; set; }

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
        Loggers.RunAll(logger => logger.Info($"Running Workflow: ({WorkflowName})"));

        var effects = EffectFactories.RunAll(factory => factory.Create());
        Metadata = await InitializeWorkflow(effects, Loggers, WorkflowName);
        await effects.RunAllAsync(context => context.SaveChanges());

        try
        {
            var result = await base.Run(input);
            Loggers.RunAll(logger => logger.Info($"({WorkflowName}) completed successfully."));

            await FinishWorkflow(Loggers, Metadata, WorkflowName, result);
            await effects.RunAllAsync(context => context.SaveChanges());

            return result;
        }
        catch (Exception e)
        {
            Loggers.RunAll(
                logger =>
                    logger.Error($"Caught Exception ({e.GetType()}) with Message ({e.Message}).")
            );

            await FinishWorkflow(Loggers, Metadata, WorkflowName, e);
            await effects.RunAllAsync(context => context.SaveChanges());

            throw;
        }
        finally
        {
            effects.RunAll(context => context.Dispose());
        }
    }

    /// <summary>
    /// initializes and begins the Workflow.
    /// </summary>
    /// <param name="workflowName"></param>
    /// <param name="effects"></param>
    /// <param name="loggers"></param>
    /// <returns></returns>
    private static async Task<Metadata> InitializeWorkflow(
        IEnumerable<IEffect> effects,
        IEnumerable<IEffectLogger> loggers,
        string workflowName
    )
    {
        loggers.RunAll(logger => logger.Info($"Initializing ({workflowName})"));

        var metadata = Metadata.Create(new CreateMetadata { Name = workflowName });
        await effects.RunAllAsync(effect => effect.Track(metadata));

        loggers.RunAll(logger => logger.Info($"Setting ({workflowName}) to In Progress."));
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
    private static async Task<Unit> FinishWorkflow(
        IEnumerable<IEffectLogger> loggers,
        Metadata metadata,
        string workflowName,
        Either<Exception, TOut> result
    )
    {
        var failureReason = result.IsRight ? null : result.Swap().ValueUnsafe();

        var resultState = result.IsRight ? WorkflowState.Completed : WorkflowState.Failed;
        loggers.RunAll(
            logger => logger.Info($"Setting ({workflowName}) to ({resultState.ToString()}).")
        );
        metadata.WorkflowState = resultState;
        metadata.EndTime = DateTime.UtcNow;

        if (failureReason != null)
            metadata.AddException(failureReason);

        return Unit.Default;
    }

    protected abstract override Task<Either<Exception, TOut>> RunInternal(TIn input);
}

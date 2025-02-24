using ChainSharp.Logging.Enums;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Models.Metadata.DTOs;
using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using ChainSharp.Logging.Services.WorkflowLogger;
using ChainSharp.Workflow;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Logging.Services.LoggedWorkflow;

/// <summary>
/// Adds information to the database about a given workflow being run.
/// </summary>
/// <typeparam name="TIn"></typeparam>
/// <typeparam name="TOut"></typeparam>
public abstract class LoggedWorkflow<TIn, TOut>(
    ILoggingProviderContextFactory contextFactory,
    IWorkflowLogger logger
) : Workflow<TIn, TOut>, ILoggedWorkflow<TIn, TOut>
{
    /// <summary>
    /// DataContextFactory for all connections required in the Workflow
    /// </summary>
    protected internal ILoggingProviderContextFactory LoggingProviderContextFactory { get; } =
        contextFactory;

    private string WorkflowName => GetType().Name;

    /// <summary>
    /// Overrides base Workflow Run to also include details about the workflow itself in
    /// the database, specifically the `workflow.workflow` table.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public new virtual async Task<TOut> Run(TIn input)
    {
        logger.Info($"Running Workflow ({WorkflowName}).");
        var context = LoggingProviderContextFactory.Create();
        var metadata = await InitializeWorkflow(context, logger, WorkflowName);
        await context.SaveChanges();

        try
        {
            logger.Info($"Running ({WorkflowName})");
            var result = await base.Run(input);
            logger.Info($"({WorkflowName}) completed successfully");

            await FinishWorkflow(logger, metadata, WorkflowName, result);
            await context.SaveChanges();

            return result;
        }
        catch (Exception e)
        {
            logger.Error($"Caught Exception ({e.GetType()}) with Message ({e.Message}).");

            await FinishWorkflow(logger, metadata, WorkflowName, e);
            await context.SaveChanges();

            throw;
        }
        finally
        {
            context.Dispose();
        }
    }

    private static async Task<Metadata> InitializeWorkflow(
        ILoggingProviderContext context,
        IWorkflowLogger logger,
        string workflowName
    )
    {
        logger.Info($"Initializing ({workflowName}");

        var metadata = Metadata.Create(context, new CreateMetadata { Name = workflowName });

        logger.Info($"Setting ({workflowName}) to In Progress.");
        metadata.WorkflowState = WorkflowState.InProgress;

        return metadata;
    }

    private static async Task<Unit> FinishWorkflow(
        IWorkflowLogger logger,
        Metadata metadata,
        string workflowName,
        Either<Exception, TOut> result
    )
    {
        var failureReason = result.IsRight ? null : result.Swap().ValueUnsafe();

        var resultState = result.IsRight ? WorkflowState.Completed : WorkflowState.Failed;
        logger.Info($"Setting ({workflowName}) to ({resultState.ToString()}).");

        if (failureReason != null)
            metadata.AddException(failureReason);

        return Unit.Default;
    }

    protected abstract override Task<Either<Exception, TOut>> RunInternal(TIn input);
}

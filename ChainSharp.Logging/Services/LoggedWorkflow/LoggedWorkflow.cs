using ChainSharp.Logging.Enums;
using ChainSharp.Logging.Models;
using ChainSharp.Logging.Services.ChainSharpProvider;
using ChainSharp.Workflow;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Logging.Services.LoggedWorkflow;

/// <summary>
/// Adds information to the database about a given workflow being run.
/// </summary>
/// <typeparam name="TIn"></typeparam>
/// <typeparam name="TOut"></typeparam>
public abstract class LoggedWorkflow<TIn, TOut>(IChainSharpProvider provider)
    : Workflow<TIn, TOut>, ILoggedWorkflow<TIn, TOut>
{
    public WorkflowMetadata Workflow { get; private set; }

    /// <summary>
    /// Will run the Workflow without a transaction, meaning all changes
    /// will be applied to the database if a transaction is not active.
    ///
    /// It is recommended to run this Function only if there is an active
    /// transaction.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<TOut> Run(TIn input)
    {
        await InitializeWorkflow();

        try
        {
            var result = await base.Run(input);

            await FinishWorkflow(result);

            return result;
        }
        catch (Exception e)
        {
            await FinishWorkflow(e);
            throw;
        }
    }

    /// <summary>
    /// Overrides base Workflow Run to also include details about the workflow itself in
    /// the database, specifically the `workflow.workflow` table.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public virtual async Task<TOut> RunWithTransaction(TIn input)
    {
        await InitializeWorkflow();

        var transaction = await provider.BeginTransaction(CancellationToken.None);

        try
        {
            var result = await base.Run(input);

            await provider.SaveChanges();
            await transaction.CommitAsync();

            await FinishWorkflow(result);

            return result;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();

            await FinishWorkflow(e);

            throw;
        }
    }

    public async Task InitializeWorkflow()
    {
        if (Workflow is not null)
            return;

        var workflowName = GetType().Name;

        Workflow = WorkflowMetadata.Create(
            provider,
            workflowName: workflowName
            );

        Workflow.WorkflowState = WorkflowState.InProgress;

        await provider.SaveChanges();
    }

    private async Task FinishWorkflow(
        Either<Exception, TOut> result)
    {
        if (Workflow is null)
            throw new NullReferenceException(
                "Workflow cannot be null. Was it initialized correctly?"
            );

        if (result.IsLeft)
        {
            Workflow.AddException(result.Swap().ValueUnsafe());
            Workflow.WorkflowState = WorkflowState.Failed;
        }
        else
            Workflow.WorkflowState = WorkflowState.Completed;

        Workflow.EndTime = DateTime.UtcNow;
        Workflow.Changes = provider.Changes;

        await provider.SaveChanges();
    }

    protected abstract override Task<Either<Exception, TOut>> RunInternal(TIn input);
}
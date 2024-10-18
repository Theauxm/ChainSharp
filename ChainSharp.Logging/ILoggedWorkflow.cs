using ChainSharp.Logging.Models;
using ChainSharp.Workflow;

namespace ChainSharp.Logging;

public interface ILoggedWorkflow<in TIn, TOut> : IWorkflow<TIn, TOut>
{
    /// <summary>
    /// Will run the Workflow without a transaction, meaning all changes
    /// will be applied to the database if a transaction is not active.
    ///
    /// It is recommended to run this Function only if there is an active
    /// transaction.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    Task<TOut> RunWithTransaction(TIn input);

    /// <summary>
    /// Overrides base Workflow Run to also include details about the workflow itself in
    /// the database, specifically the `workflow.workflow` table.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    new Task<TOut> Run(TIn input);

    public WorkflowMetadata Workflow { get; }

    public Task InitializeWorkflow();
}

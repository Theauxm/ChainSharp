using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Workflow;

namespace ChainSharp.Effect.Services.EffectWorkflow;

/// <summary>
/// Defines the contract for workflows that include database tracking and logging capabilities.
/// This interface extends the base IWorkflow interface to add metadata tracking.
/// </summary>
/// <typeparam name="TIn">The input type for the workflow</typeparam>
/// <typeparam name="TOut">The output type for the workflow</typeparam>
/// <remarks>
/// IEffectWorkflow is the interface representation of the EffectWorkflow class.
/// It allows for dependency injection and testing of workflows with database tracking.
/// </remarks>
public interface IEffectWorkflow<in TIn, TOut> : IWorkflow<TIn, TOut>, IDisposable
{
    /// <summary>
    /// Executes the workflow with the given input and records execution details in the database.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>The result of the workflow execution</returns>
    /// <remarks>
    /// This method overrides the base Run method from IWorkflow to add database tracking.
    /// It records workflow execution details including inputs, outputs, timing, and error information.
    /// </remarks>
    new Task<TOut> Run(TIn input);

    /// <summary>
    /// Gets the metadata associated with this workflow execution.
    /// Contains tracking information such as state, timing, and error details.
    /// </summary>
    /// <remarks>
    /// This property provides access to the database record that tracks this workflow execution.
    /// It can be used to query the current state of the workflow or to access execution details.
    /// </remarks>
    public Metadata? Metadata { get; }
}

using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Workflow;

namespace ChainSharp.Effect.Services.EffectWorkflow;

public interface IEffectWorkflow<in TIn, TOut> : IWorkflow<TIn, TOut>
{
    /// <summary>
    /// Overrides base Workflow Run to also include details about the workflow itself in
    /// the database, specifically the `workflow.workflow` table.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    new Task<TOut> Run(TIn input);

    public Metadata Metadata { get; }
}

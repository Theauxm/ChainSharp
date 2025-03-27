using ChainSharp.Effect.Services.EffectWorkflow;

namespace ChainSharp.Effect.Mediator.Services.WorkflowBus;

public interface IWorkflowBus
{
    public Task<TOut> RunAsync<TIn, TOut>(TIn workflowInput);
}

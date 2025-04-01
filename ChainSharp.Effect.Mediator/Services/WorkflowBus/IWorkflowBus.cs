using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Workflow;

namespace ChainSharp.Effect.Mediator.Services.WorkflowBus;

public interface IWorkflowBus
{
    public Task<TOut> RunAsync<TOut>(object workflowInput, Metadata? metadata = null);
}

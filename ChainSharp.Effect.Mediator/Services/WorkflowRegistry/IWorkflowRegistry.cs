namespace ChainSharp.Effect.Mediator.Services.WorkflowRegistry;

public interface IWorkflowRegistry
{
    public Dictionary<Type, Type> InputTypeToWorkflow { get; set; }
}

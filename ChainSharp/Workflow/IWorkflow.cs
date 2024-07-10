namespace ChainSharp.Workflow;

public interface IWorkflow<in TInput, TReturn>
{
    public Task<TReturn> Run(TInput input);
}
using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.HelloWorld;

/// <summary>
/// Interface for the HelloWorld workflow.
/// Used by the WorkflowBus for workflow resolution.
/// </summary>
public interface IHelloWorldWorkflow : IServiceTrain<HelloWorldInput, Unit>;

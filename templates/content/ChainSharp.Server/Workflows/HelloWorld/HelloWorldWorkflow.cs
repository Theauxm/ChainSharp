using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Server.Workflows.HelloWorld.Steps;
using LanguageExt;

namespace ChainSharp.Server.Workflows.HelloWorld;

/// <summary>
/// A simple "Hello World" workflow that demonstrates scheduled execution.
/// This workflow takes a name as input and logs a greeting message.
/// </summary>
public class HelloWorldWorkflow : EffectWorkflow<HelloWorldInput, Unit>, IHelloWorldWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(HelloWorldInput input) =>
        Activate(input).Chain<LogGreetingStep>().Resolve();
}

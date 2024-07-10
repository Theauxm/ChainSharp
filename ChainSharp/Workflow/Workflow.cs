using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public abstract partial class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    protected internal Exception? Exception { get; set; }

    protected internal Dictionary<Type, object> Memory { get; private set; } = null!;

    private TReturn? ShortCircuitValue { get; set; }

    public async Task<TReturn> Run(TInput input) => await RunEither(input).Unwrap();

    public Task<Either<Exception, TReturn>> RunEither(TInput input)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory = new Dictionary<Type, object> { { typeof(Unit), Unit.Default } };

        return RunInternal(input);
    }

    protected abstract Task<Either<Exception, TReturn>> RunInternal(TInput input);
}

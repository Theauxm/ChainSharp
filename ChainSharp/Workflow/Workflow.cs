using ChainSharp.Extensions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Workflow;

public abstract partial class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    protected internal Exception? Exception { get; set; }

    protected internal Dictionary<Type, object> Memory { get; private set; } = null!;

    private TReturn ShortCircuitValue { get; set; } = default!;
    private bool ShortCircuitValueSet { get; set; } = false;

    public async Task<TReturn> Run(TInput input)
    {
        var resultEither = await RunEither(input);

        if (resultEither.IsLeft)
            resultEither.Swap().ValueUnsafe().Rethrow();

        return resultEither.Unwrap();
    }
    
    public async Task<TReturn> Run(TInput input, params object[] args)
    {
        var resultEither = await RunEither(input);

        if (resultEither.IsLeft)
            resultEither.Swap().ValueUnsafe().Rethrow();

        return resultEither.Unwrap();
    }

    public Task<Either<Exception, TReturn>> RunEither(TInput input)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object> { { typeof(Unit), Unit.Default } };

        return RunInternal(input);
    }

    protected abstract Task<Either<Exception, TReturn>> RunInternal(TInput input);
}

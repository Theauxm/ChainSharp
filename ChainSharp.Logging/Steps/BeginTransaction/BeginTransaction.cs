using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Logging.Steps.BeginTransaction;

/// <summary>
/// Built-in step allowing for transactions to occur.
/// </summary>
public class BeginTransaction(ILoggingProviderContext loggingProviderContext) : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        await loggingProviderContext.BeginTransaction();

        return Unit.Default;
    }
}

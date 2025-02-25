using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Logging.Steps.CommitTransaction;

/// <summary>
/// Built-in step allowing for transactions to be committed.
/// </summary>
public class CommitTransaction(ILoggingProviderContext loggingProviderContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        await loggingProviderContextFactory.CommitTransaction();

        return Unit.Default;
    }
}

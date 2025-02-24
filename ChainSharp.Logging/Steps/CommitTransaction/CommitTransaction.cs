using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Logging.Steps.CommitTransaction;

public class CommitTransaction(ILoggingProviderContext loggingProviderContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        await loggingProviderContextFactory.CommitTransaction();

        return Unit.Default;
    }
}

using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Logging.Steps.BeginTransaction;

public class BeginTransaction(ILoggingProviderContext loggingProviderContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        await loggingProviderContextFactory.BeginTransaction();

        return Unit.Default;
    }
}

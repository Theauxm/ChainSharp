using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Effect.Data.Steps.BeginTransaction;

/// <summary>
/// Built-in step allowing for transactions to occur.
/// </summary>
public class BeginTransaction(IDataContext dataContext) : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        await dataContext.BeginTransaction();

        return Unit.Default;
    }
}

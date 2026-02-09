using ChainSharp.Workflow;
using LanguageExt;

namespace ChainSharp.Tests.Unit.Utils;

public class UnitWorkflow : Workflow<LanguageExt.Unit, LanguageExt.Unit>
{
    protected override async Task<Either<Exception, LanguageExt.Unit>> RunInternal(
        LanguageExt.Unit input
    ) => Activate(input).Resolve();

    public static UnitWorkflow Create() => new();
}

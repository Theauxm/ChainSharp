using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Tests.Benchmarks.Models;
using ChainSharp.Tests.Benchmarks.Steps;
using LanguageExt;

namespace ChainSharp.Tests.Benchmarks.Workflows;

// --- Interfaces ---

public interface IEffectAddOneWorkflow : IEffectWorkflow<int, int>;

public interface IEffectAddThreeWorkflow : IEffectWorkflow<int, int>;

public interface IEffectTransformWorkflow : IEffectWorkflow<PersonDto, PersonEntity>;

public interface IEffectSimulatedIoWorkflow : IEffectWorkflow<int, int>;

public interface IEffectAddOneX1Workflow : IEffectWorkflow<int, int>;

public interface IEffectAddOneX3Workflow : IEffectWorkflow<int, int>;

public interface IEffectAddOneX5Workflow : IEffectWorkflow<int, int>;

public interface IEffectAddOneX10Workflow : IEffectWorkflow<int, int>;

// --- Implementations ---

public class EffectAddOneWorkflow : EffectWorkflow<int, int>, IEffectAddOneWorkflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(Activate(input).Chain<AddOneStep>().Resolve());
}

public class EffectAddThreeWorkflow : EffectWorkflow<int, int>, IEffectAddThreeWorkflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(
            Activate(input).Chain<AddOneStep>().Chain<AddOneStep>().Chain<AddOneStep>().Resolve()
        );
}

public class EffectTransformWorkflow
    : EffectWorkflow<PersonDto, PersonEntity>,
        IEffectTransformWorkflow
{
    protected override Task<Either<Exception, PersonEntity>> RunInternal(PersonDto input) =>
        Task.FromResult(Activate(input).Chain<TransformStep>().Resolve());
}

public class EffectSimulatedIoWorkflow : EffectWorkflow<int, int>, IEffectSimulatedIoWorkflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(
            Activate(input)
                .Chain<SimulatedIoStep>()
                .Chain<SimulatedIoStep>()
                .Chain<SimulatedIoStep>()
                .Resolve()
        );
}

// --- Scaling variants ---

public class EffectAddOneX1Workflow : EffectWorkflow<int, int>, IEffectAddOneX1Workflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(Activate(input).Chain<AddOneStep>().Resolve());
}

public class EffectAddOneX3Workflow : EffectWorkflow<int, int>, IEffectAddOneX3Workflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(
            Activate(input).Chain<AddOneStep>().Chain<AddOneStep>().Chain<AddOneStep>().Resolve()
        );
}

public class EffectAddOneX5Workflow : EffectWorkflow<int, int>, IEffectAddOneX5Workflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(
            Activate(input)
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Resolve()
        );
}

public class EffectAddOneX10Workflow : EffectWorkflow<int, int>, IEffectAddOneX10Workflow
{
    protected override Task<Either<Exception, int>> RunInternal(int input) =>
        Task.FromResult(
            Activate(input)
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Chain<AddOneStep>()
                .Resolve()
        );
}

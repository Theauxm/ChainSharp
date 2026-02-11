using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

#pragma warning disable CS8766 // Nullability mismatch on Metadata property inherited from EffectWorkflow

namespace ChainSharp.Tests.Effect.Dashboard.Integration.Fakes;

// --- Simple workflow A ---
public record FakeInputA;

public interface IFakeWorkflowA : IEffectWorkflow<FakeInputA, string> { }

public class FakeWorkflowA : EffectWorkflow<FakeInputA, string>, IFakeWorkflowA
{
    protected override Task<Either<Exception, string>> RunInternal(FakeInputA input) =>
        Task.FromResult<Either<Exception, string>>("ok");
}

// --- Simple workflow B ---
public record FakeInputB;

public interface IFakeWorkflowB : IEffectWorkflow<FakeInputB, int> { }

public class FakeWorkflowB : EffectWorkflow<FakeInputB, int>, IFakeWorkflowB
{
    protected override Task<Either<Exception, int>> RunInternal(FakeInputB input) =>
        Task.FromResult<Either<Exception, int>>(0);
}

// --- Simple workflow C ---
public record FakeInputC;

public interface IFakeWorkflowC : IEffectWorkflow<FakeInputC, bool> { }

public class FakeWorkflowC : EffectWorkflow<FakeInputC, bool>, IFakeWorkflowC
{
    protected override Task<Either<Exception, bool>> RunInternal(FakeInputC input) =>
        Task.FromResult<Either<Exception, bool>>(true);
}

// --- Workflow with generic input/output types for friendly name tests ---
public interface IFakeGenericWorkflow : IEffectWorkflow<List<string>, Dictionary<string, int>> { }

public class FakeGenericWorkflow
    : EffectWorkflow<List<string>, Dictionary<string, int>>,
        IFakeGenericWorkflow
{
    protected override Task<Either<Exception, Dictionary<string, int>>> RunInternal(
        List<string> input
    ) => Task.FromResult<Either<Exception, Dictionary<string, int>>>(new Dictionary<string, int>());
}

// --- Non-workflow service for negative tests ---
public interface INotAWorkflow
{
    string DoSomething();
}

public class NotAWorkflow : INotAWorkflow
{
    public string DoSomething() => "not a workflow";
}

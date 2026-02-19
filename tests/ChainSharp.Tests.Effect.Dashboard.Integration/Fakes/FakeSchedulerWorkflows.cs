using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

#pragma warning disable CS8766 // Nullability mismatch on Metadata property inherited from EffectWorkflow

namespace ChainSharp.Tests.Effect.Dashboard.Integration.Fakes;

// --- Manifest-compatible fakes for scheduler builder tests ---
// These satisfy TWorkflow : IEffectWorkflow<TInput, Unit> where TInput : IManifestProperties

public record FakeManifestInputA : IManifestProperties;

public interface IFakeSchedulerWorkflowA : IEffectWorkflow<FakeManifestInputA, Unit> { }

public class FakeSchedulerWorkflowA
    : EffectWorkflow<FakeManifestInputA, Unit>,
        IFakeSchedulerWorkflowA
{
    protected override Task<Either<Exception, Unit>> RunInternal(FakeManifestInputA input) =>
        Task.FromResult<Either<Exception, Unit>>(Unit.Default);
}

public record FakeManifestInputB : IManifestProperties;

public interface IFakeSchedulerWorkflowB : IEffectWorkflow<FakeManifestInputB, Unit> { }

public class FakeSchedulerWorkflowB
    : EffectWorkflow<FakeManifestInputB, Unit>,
        IFakeSchedulerWorkflowB
{
    protected override Task<Either<Exception, Unit>> RunInternal(FakeManifestInputB input) =>
        Task.FromResult<Either<Exception, Unit>>(Unit.Default);
}

public record FakeManifestInputC : IManifestProperties;

public interface IFakeSchedulerWorkflowC : IEffectWorkflow<FakeManifestInputC, Unit> { }

public class FakeSchedulerWorkflowC
    : EffectWorkflow<FakeManifestInputC, Unit>,
        IFakeSchedulerWorkflowC
{
    protected override Task<Either<Exception, Unit>> RunInternal(FakeManifestInputC input) =>
        Task.FromResult<Either<Exception, Unit>>(Unit.Default);
}

public record FakeManifestInputD : IManifestProperties;

public interface IFakeSchedulerWorkflowD : IEffectWorkflow<FakeManifestInputD, Unit> { }

public class FakeSchedulerWorkflowD
    : EffectWorkflow<FakeManifestInputD, Unit>,
        IFakeSchedulerWorkflowD
{
    protected override Task<Either<Exception, Unit>> RunInternal(FakeManifestInputD input) =>
        Task.FromResult<Either<Exception, Unit>>(Unit.Default);
}

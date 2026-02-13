using ChainSharp.Effect.Models;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Services.EffectRegistry;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.Services.StepEffectRunner;
using FluentAssertions;
using Moq;

namespace ChainSharp.Tests.Effect.Integration.UnitTests.Services;

[TestFixture]
public class EffectRunnerRegistryTests
{
    #region EffectRunner

    [Test]
    public void EffectRunner_DisabledFactory_SkipsCreateCall()
    {
        // Arrange
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IEffectProviderFactory>();
        registry.Register(mockFactory.Object.GetType(), enabled: false);

        // Act
        using var runner = new EffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Never);
    }

    [Test]
    public void EffectRunner_EnabledFactory_CallsCreate()
    {
        // Arrange
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IEffectProviderFactory>();
        var mockProvider = new Mock<IEffectProvider>();
        mockFactory.Setup(f => f.Create()).Returns(mockProvider.Object);
        registry.Register(mockFactory.Object.GetType(), enabled: true);

        // Act
        using var runner = new EffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Once);
    }

    [Test]
    public void EffectRunner_UntrackedFactory_CallsCreate()
    {
        // Arrange - factory type not registered in registry (infrastructure effect)
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IEffectProviderFactory>();
        var mockProvider = new Mock<IEffectProvider>();
        mockFactory.Setup(f => f.Create()).Returns(mockProvider.Object);
        // Intentionally NOT registering the factory type

        // Act
        using var runner = new EffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Once);
    }

    [Test]
    public void EffectRunner_MixOfEnabledAndDisabled_OnlyCreatesEnabled()
    {
        // Arrange - use concrete stub types so GetType() returns distinct types
        var registry = new EffectRegistry();
        var enabledFactory = new EnabledEffectFactory();
        var disabledFactory = new DisabledEffectFactory();

        registry.Register(typeof(EnabledEffectFactory), enabled: true);
        registry.Register(typeof(DisabledEffectFactory), enabled: false);

        // Act
        using var runner = new EffectRunner([enabledFactory, disabledFactory], registry);

        // Assert
        enabledFactory.CreateCalled.Should().BeTrue();
        disabledFactory.CreateCalled.Should().BeFalse();
    }

    [Test]
    public void EffectRunner_NonToggleableFactory_CannotBeDisabled()
    {
        // Arrange - register as non-toggleable, then try to disable
        var registry = new EffectRegistry();
        var factory = new EnabledEffectFactory();
        registry.Register(typeof(EnabledEffectFactory), enabled: true, toggleable: false);

        // Attempt to disable should be a no-op
        registry.Disable(typeof(EnabledEffectFactory));

        // Act
        using var runner = new EffectRunner([factory], registry);

        // Assert - factory should still run because it can't be disabled
        factory.CreateCalled.Should().BeTrue();
    }

    #endregion

    #region StepEffectRunner

    [Test]
    public void StepEffectRunner_DisabledFactory_SkipsCreateCall()
    {
        // Arrange
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IStepEffectProviderFactory>();
        registry.Register(mockFactory.Object.GetType(), enabled: false);

        // Act
        using var runner = new StepEffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Never);
    }

    [Test]
    public void StepEffectRunner_EnabledFactory_CallsCreate()
    {
        // Arrange
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IStepEffectProviderFactory>();
        var mockProvider = new Mock<IStepEffectProvider>();
        mockFactory.Setup(f => f.Create()).Returns(mockProvider.Object);
        registry.Register(mockFactory.Object.GetType(), enabled: true);

        // Act
        using var runner = new StepEffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Once);
    }

    [Test]
    public void StepEffectRunner_UntrackedFactory_CallsCreate()
    {
        // Arrange - factory type not registered in registry (infrastructure effect)
        var registry = new EffectRegistry();
        var mockFactory = new Mock<IStepEffectProviderFactory>();
        var mockProvider = new Mock<IStepEffectProvider>();
        mockFactory.Setup(f => f.Create()).Returns(mockProvider.Object);
        // Intentionally NOT registering the factory type

        // Act
        using var runner = new StepEffectRunner([mockFactory.Object], registry);

        // Assert
        mockFactory.Verify(f => f.Create(), Times.Once);
    }

    [Test]
    public void StepEffectRunner_MixOfEnabledAndDisabled_OnlyCreatesEnabled()
    {
        // Arrange - use concrete stub types so GetType() returns distinct types
        var registry = new EffectRegistry();
        var enabledFactory = new EnabledStepEffectFactory();
        var disabledFactory = new DisabledStepEffectFactory();

        registry.Register(typeof(EnabledStepEffectFactory), enabled: true);
        registry.Register(typeof(DisabledStepEffectFactory), enabled: false);

        // Act
        using var runner = new StepEffectRunner([enabledFactory, disabledFactory], registry);

        // Assert
        enabledFactory.CreateCalled.Should().BeTrue();
        disabledFactory.CreateCalled.Should().BeFalse();
    }

    [Test]
    public void StepEffectRunner_NonToggleableFactory_CannotBeDisabled()
    {
        // Arrange - register as non-toggleable, then try to disable
        var registry = new EffectRegistry();
        var factory = new EnabledStepEffectFactory();
        registry.Register(typeof(EnabledStepEffectFactory), enabled: true, toggleable: false);

        // Attempt to disable should be a no-op
        registry.Disable(typeof(EnabledStepEffectFactory));

        // Act
        using var runner = new StepEffectRunner([factory], registry);

        // Assert - factory should still run because it can't be disabled
        factory.CreateCalled.Should().BeTrue();
    }

    #endregion

    #region Test Stubs

    // Concrete stub factories with distinct types for mix tests
    // (Moq proxies share the same GetType(), so we need real types)

    private class StubEffectProvider : IEffectProvider
    {
        public Task SaveChanges(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task Track(IModel model) => Task.CompletedTask;

        public Task Update(IModel model) => Task.CompletedTask;

        public void Dispose() { }
    }

    private class EnabledEffectFactory : IEffectProviderFactory
    {
        public bool CreateCalled { get; private set; }

        public IEffectProvider Create()
        {
            CreateCalled = true;
            return new StubEffectProvider();
        }
    }

    private class DisabledEffectFactory : IEffectProviderFactory
    {
        public bool CreateCalled { get; private set; }

        public IEffectProvider Create()
        {
            CreateCalled = true;
            return new StubEffectProvider();
        }
    }

    private class StubStepEffectProvider : IStepEffectProvider
    {
        public Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
            EffectStep<TIn, TOut> effectStep,
            EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
            EffectStep<TIn, TOut> effectStep,
            EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public void Dispose() { }
    }

    private class EnabledStepEffectFactory : IStepEffectProviderFactory
    {
        public bool CreateCalled { get; private set; }

        public IStepEffectProvider Create()
        {
            CreateCalled = true;
            return new StubStepEffectProvider();
        }
    }

    private class DisabledStepEffectFactory : IStepEffectProviderFactory
    {
        public bool CreateCalled { get; private set; }

        public IStepEffectProvider Create()
        {
            CreateCalled = true;
            return new StubStepEffectProvider();
        }
    }

    #endregion
}

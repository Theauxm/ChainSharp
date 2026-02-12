using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Json.Services.JsonEffectFactory;
using ChainSharp.Effect.Services.EffectRegistry;
using ChainSharp.Effect.StepProvider.Logging.Extensions;
using ChainSharp.Effect.StepProvider.Logging.Services.StepLoggerFactory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Integration.UnitTests.Extensions;

[TestFixture]
public class ServiceExtensionsRegistryTests
{
    [Test]
    public void AddChainSharpEffects_RegistersIEffectRegistryAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpEffects();
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetService<IEffectRegistry>();
        registry.Should().NotBeNull();

        // Verify it's a singleton (same instance)
        var registry2 = provider.GetService<IEffectRegistry>();
        registry.Should().BeSameAs(registry2);
    }

    [Test]
    public void AddEffect_DefaultToggleable_RegistersInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - AddJsonEffect calls AddEffect<JsonEffectProviderFactory>() with default toggleable=true
        services.AddChainSharpEffects(options => options.AddJsonEffect());
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetRequiredService<IEffectRegistry>();
        var all = registry.GetAll();

        all.Should().ContainKey(typeof(JsonEffectProviderFactory));
        all[typeof(JsonEffectProviderFactory)].Should().BeTrue();
    }

    [Test]
    public void AddStepEffect_DefaultToggleable_RegistersInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - AddStepLogger calls AddStepEffect<StepLoggerFactory>() with default toggleable=true
        services.AddChainSharpEffects(options => options.AddStepLogger());
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetRequiredService<IEffectRegistry>();
        var all = registry.GetAll();

        all.Should().ContainKey(typeof(StepLoggerFactory));
        all[typeof(StepLoggerFactory)].Should().BeTrue();
    }

    [Test]
    public void AddChainSharpEffects_NoEffects_RegistryIsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpEffects();
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetRequiredService<IEffectRegistry>();
        registry.GetAll().Should().BeEmpty();
    }

    [Test]
    public void AddEffect_Toggleable_AppearsInGetToggleable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddChainSharpEffects(options => options.AddJsonEffect());
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetRequiredService<IEffectRegistry>();
        var toggleable = registry.GetToggleable();

        toggleable.Should().ContainKey(typeof(JsonEffectProviderFactory));
    }

    [Test]
    public void AddChainSharpEffects_MultipleEffects_AllRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddChainSharpEffects(options => options.AddJsonEffect().AddStepLogger());
        using var provider = services.BuildServiceProvider();

        // Assert
        var registry = provider.GetRequiredService<IEffectRegistry>();
        var all = registry.GetAll();

        all.Should().HaveCount(2);
        all.Should().ContainKey(typeof(JsonEffectProviderFactory));
        all.Should().ContainKey(typeof(StepLoggerFactory));
    }
}

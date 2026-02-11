using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Services.EffectRegistry;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using FluentAssertions;

namespace ChainSharp.Tests.Effect.Integration.UnitTests.Services;

[TestFixture]
public class EffectRegistryTests
{
    private EffectRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new EffectRegistry();
    }

    #region IsEnabled

    [Test]
    public void IsEnabled_UntrackedType_ReturnsTrue()
    {
        // Untracked types should always be considered enabled (infrastructure effects)
        _registry.IsEnabled(typeof(UntrackedFactory)).Should().BeTrue();
    }

    [Test]
    public void IsEnabled_RegisteredAndEnabled_ReturnsTrue()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeTrue();
    }

    [Test]
    public void IsEnabled_RegisteredAndDisabled_ReturnsFalse()
    {
        _registry.Register(typeof(FakeFactory), enabled: false);

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeFalse();
    }

    [Test]
    public void IsEnabled_Generic_MatchesNonGeneric()
    {
        _registry.Register(typeof(FakeFactory), enabled: false);

        _registry.IsEnabled<FakeFactory>().Should().Be(_registry.IsEnabled(typeof(FakeFactory)));
    }

    #endregion

    #region Enable / Disable

    [Test]
    public void Enable_PreviouslyDisabled_SetsEnabled()
    {
        _registry.Register(typeof(FakeFactory), enabled: false);
        _registry.IsEnabled(typeof(FakeFactory)).Should().BeFalse();

        _registry.Enable(typeof(FakeFactory));

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeTrue();
    }

    [Test]
    public void Disable_PreviouslyEnabled_SetsDisabled()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);
        _registry.IsEnabled(typeof(FakeFactory)).Should().BeTrue();

        _registry.Disable(typeof(FakeFactory));

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeFalse();
    }

    [Test]
    public void Enable_Generic_MatchesNonGeneric()
    {
        _registry.Register(typeof(FakeFactory), enabled: false);

        _registry.Enable<FakeFactory>();

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeTrue();
    }

    [Test]
    public void Disable_Generic_MatchesNonGeneric()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);

        _registry.Disable<FakeFactory>();

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeFalse();
    }

    #endregion

    #region Register

    [Test]
    public void Register_CalledTwice_DoesNotOverwriteExistingState()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);
        _registry.Disable(typeof(FakeFactory));

        // Second register should not overwrite the disabled state
        _registry.Register(typeof(FakeFactory), enabled: true);

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeFalse();
    }

    [Test]
    public void Register_DefaultEnabled_IsTrue()
    {
        _registry.Register(typeof(FakeFactory));

        _registry.IsEnabled(typeof(FakeFactory)).Should().BeTrue();
    }

    #endregion

    #region GetAll

    [Test]
    public void GetAll_ReturnsAllTrackedTypes()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);
        _registry.Register(typeof(AnotherFakeFactory), enabled: false);

        var all = _registry.GetAll();

        all.Should().HaveCount(2);
        all[typeof(FakeFactory)].Should().BeTrue();
        all[typeof(AnotherFakeFactory)].Should().BeFalse();
    }

    [Test]
    public void GetAll_EmptyRegistry_ReturnsEmpty()
    {
        _registry.GetAll().Should().BeEmpty();
    }

    [Test]
    public void GetAll_ReturnsSnapshot_MutationsDoNotAffectResult()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);
        var snapshot = _registry.GetAll();

        _registry.Disable(typeof(FakeFactory));

        // The snapshot should still show the original state
        snapshot[typeof(FakeFactory)].Should().BeTrue();
    }

    #endregion

    #region Thread Safety

    [Test]
    public void ConcurrentEnableDisable_DoesNotThrow()
    {
        _registry.Register(typeof(FakeFactory), enabled: true);

        var tasks = Enumerable
            .Range(0, 100)
            .Select(
                i =>
                    Task.Run(() =>
                    {
                        if (i % 2 == 0)
                            _registry.Enable(typeof(FakeFactory));
                        else
                            _registry.Disable(typeof(FakeFactory));

                        _ = _registry.IsEnabled(typeof(FakeFactory));
                        _ = _registry.GetAll();
                    })
            )
            .ToArray();

        var act = () => Task.WaitAll(tasks);

        act.Should().NotThrow();
    }

    [Test]
    public void ConcurrentRegisterAndQuery_DoesNotThrow()
    {
        var tasks = Enumerable
            .Range(0, 100)
            .Select(
                i =>
                    Task.Run(() =>
                    {
                        // Use a unique type per iteration via a dictionary lookup
                        _registry.Register(typeof(FakeFactory), enabled: i % 2 == 0);
                        _ = _registry.IsEnabled(typeof(FakeFactory));
                        _ = _registry.GetAll();
                    })
            )
            .ToArray();

        var act = () => Task.WaitAll(tasks);

        act.Should().NotThrow();
    }

    #endregion

    #region Test Helpers

    // Dummy types for testing - never actually instantiated
    private class FakeFactory;

    private class AnotherFakeFactory;

    private class UntrackedFactory;

    #endregion
}

using ChainSharp.Effect.Data.InMemory.Extensions;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Provider.Alerting.Extensions;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ChainSharp.Tests.Effect.Provider.Alerting.Integration;

[TestFixture]
public class AlertingIntegrationTests
{
    private ServiceProvider? _serviceProvider;
    private TestAlertSender? _testSender;

    [SetUp]
    public void SetUp()
    {
        _testSender = new TestAlertSender();
        
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IAlertSender>(_testSender);
        
        services.AddChainSharpEffects(options =>
            options
                .AddInMemoryEffect()
                .AddEffectWorkflowBus(assemblies: typeof(AlertingIntegrationTests).Assembly)
                .UseAlertingEffect(alertOptions =>
                    alertOptions.AddAlertSender<TestAlertSender>())
        );
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
        _testSender?.Reset();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AlertConfigurationBuilder Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void AlertConfigurationBuilder_BothFieldsSet_BuildsSuccessfully()
    {
        var config = AlertConfigurationBuilder.Create()
            .WithinTimeSpan(TimeSpan.FromHours(1))
            .MinimumFailures(3)
            .Build();

        Assert.That(config.TimeWindow, Is.EqualTo(TimeSpan.FromHours(1)));
        Assert.That(config.MinimumFailures, Is.EqualTo(3));
    }

    [Test]
    public void AlertConfigurationBuilder_AlertOnEveryFailure_SetsCorrectValues()
    {
        var config = AlertConfigurationBuilder.Create()
            .AlertOnEveryFailure()
            .Build();

        Assert.That(config.TimeWindow, Is.EqualTo(TimeSpan.Zero));
        Assert.That(config.MinimumFailures, Is.EqualTo(1));
    }

    [Test]
    public void AlertConfigurationBuilder_MissingTimeWindow_ThrowsException()
    {
        var builder = AlertConfigurationBuilder.Create()
            .MinimumFailures(3);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void AlertConfigurationBuilder_MissingMinimumFailures_ThrowsException()
    {
        var builder = AlertConfigurationBuilder.Create()
            .WithinTimeSpan(TimeSpan.FromHours(1));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void AlertConfigurationBuilder_WithFilters_StoresFiltersCorrectly()
    {
        var config = AlertConfigurationBuilder.Create()
            .WithinTimeSpan(TimeSpan.FromHours(1))
            .MinimumFailures(3)
            .WhereExceptionType<TimeoutException>()
            .WhereFailureStepNameEquals("Step1")
            .WhereFailureStepName(s => s.StartsWith("DB"))
            .AndCustomFilter(m => m.FailureReason != null)
            .Build();

        Assert.That(config.ExceptionTypes, Has.Count.EqualTo(1));
        Assert.That(config.ExceptionTypes[0], Is.EqualTo(typeof(TimeoutException)));
        Assert.That(config.StepFilters, Has.Count.EqualTo(2));
        Assert.That(config.CustomFilters, Has.Count.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════
    // AlertConfigurationRegistry Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void AlertConfigurationRegistry_ScansAndRegisters_IAlertingWorkflows()
    {
        var registry = _serviceProvider!.GetRequiredService<IAlertConfigurationRegistry>();
        
        var config = registry.GetConfiguration(typeof(SimpleAlertWorkflow).FullName!);
        
        Assert.That(config, Is.Not.Null);
        Assert.That(config!.MinimumFailures, Is.EqualTo(1));
    }

    [Test]
    public void AlertConfigurationRegistry_NonAlertingWorkflow_ReturnsNull()
    {
        var registry = _serviceProvider!.GetRequiredService<IAlertConfigurationRegistry>();
        
        var config = registry.GetConfiguration(typeof(NonAlertingWorkflow).FullName!);
        
        Assert.That(config, Is.Null);
    }

    // ═══════════════════════════════════════════════════════════════════
    // OnError Hook Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task OnError_AlertingWorkflowFails_SendsAlert()
    {
        var workflow = _serviceProvider!.GetRequiredService<ISimpleAlertWorkflow>();

        try
        {
            await workflow.Run(new SimpleInput { ShouldFail = true });
        }
        catch
        {
            // Expected to throw
        }

        await Task.Delay(100); // Give async operations time to complete

        Assert.That(_testSender!.AlertsSent, Has.Count.EqualTo(1));
        Assert.That(_testSender.AlertsSent[0].WorkflowName, Does.Contain("SimpleAlertWorkflow"));
        Assert.That(_testSender.AlertsSent[0].FailureCount, Is.EqualTo(1));
    }

    [Test]
    public async Task OnError_NonAlertingWorkflowFails_NoAlert()
    {
        var workflow = _serviceProvider!.GetRequiredService<INonAlertingWorkflow>();

        try
        {
            await workflow.Run(new SimpleInput { ShouldFail = true });
        }
        catch
        {
            // Expected to throw
        }

        await Task.Delay(100);

        Assert.That(_testSender!.AlertsSent, Is.Empty);
    }

    [Test]
    public async Task OnError_AlertingWorkflowSucceeds_NoAlert()
    {
        var workflow = _serviceProvider!.GetRequiredService<ISimpleAlertWorkflow>();

        await workflow.Run(new SimpleInput { ShouldFail = false });

        Assert.That(_testSender!.AlertsSent, Is.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Filter Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task Filter_ExceptionTypeMatches_SendsAlert()
    {
        var workflow = _serviceProvider!.GetRequiredService<ITimeoutFilterWorkflow>();

        try
        {
            await workflow.Run(new SimpleInput 
            { 
                ShouldFail = true,
                ExceptionType = "TimeoutException" 
            });
        }
        catch { }

        await Task.Delay(100);

        Assert.That(_testSender!.AlertsSent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Filter_ExceptionTypeDoesNotMatch_NoAlert()
    {
        var workflow = _serviceProvider!.GetRequiredService<ITimeoutFilterWorkflow>();

        try
        {
            await workflow.Run(new SimpleInput 
            { 
                ShouldFail = true,
                ExceptionType = "ArgumentException" // Different exception
            });
        }
        catch { }

        await Task.Delay(100);

        Assert.That(_testSender!.AlertsSent, Is.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AlertContext Validation Tests
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task AlertContext_ContainsExpectedData()
    {
        var workflow = _serviceProvider!.GetRequiredService<ISimpleAlertWorkflow>();

        try
        {
            await workflow.Run(new SimpleInput { ShouldFail = true });
        }
        catch { }

        await Task.Delay(100);

        var context = _testSender!.AlertsSent[0];
        
        Assert.That(context.WorkflowName, Is.Not.Null);
        Assert.That(context.TriggerMetadata, Is.Not.Null);
        Assert.That(context.TriggerMetadata.WorkflowState, Is.EqualTo(WorkflowState.Failed));
        Assert.That(context.FailureCount, Is.EqualTo(1));
        Assert.That(context.FailedExecutions, Has.Count.EqualTo(1));
        Assert.That(context.ExceptionFrequency, Is.Not.Empty);
        Assert.That(context.FailedStepFrequency, Is.Not.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Test Workflows and Support Classes
    // ═══════════════════════════════════════════════════════════════════

    public record SimpleInput : IManifestProperties
    {
        public bool ShouldFail { get; init; }
        public string ExceptionType { get; init; } = "Exception";
    }

    public interface ISimpleAlertWorkflow : IAlertingWorkflow<SimpleInput, Unit> { }
    
    public class SimpleAlertWorkflow : EffectWorkflow<SimpleInput, Unit>, ISimpleAlertWorkflow
    {
        public AlertConfiguration ConfigureAlerting() =>
            AlertConfigurationBuilder.Create().AlertOnEveryFailure().Build();

        protected override async Task<Either<Exception, Unit>> RunInternal(SimpleInput input)
        {
            if (input.ShouldFail)
                throw new Exception("Intentional failure for testing");
            
            return Unit.Default;
        }
    }

    public interface INonAlertingWorkflow : IEffectWorkflow<SimpleInput, Unit> { }
    
    public class NonAlertingWorkflow : EffectWorkflow<SimpleInput, Unit>, INonAlertingWorkflow
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(SimpleInput input)
        {
            if (input.ShouldFail)
                throw new Exception("Intentional failure for testing");
            
            return Unit.Default;
        }
    }

    public interface ITimeoutFilterWorkflow : IAlertingWorkflow<SimpleInput, Unit> { }
    
    public class TimeoutFilterWorkflow : EffectWorkflow<SimpleInput, Unit>, ITimeoutFilterWorkflow
    {
        public AlertConfiguration ConfigureAlerting() =>
            AlertConfigurationBuilder.Create()
                .AlertOnEveryFailure()
                .WhereExceptionType<TimeoutException>()
                .Build();

        protected override async Task<Either<Exception, Unit>> RunInternal(SimpleInput input)
        {
            if (input.ShouldFail)
            {
                throw input.ExceptionType switch
                {
                    "TimeoutException" => new TimeoutException("Test timeout"),
                    "ArgumentException" => new ArgumentException("Test argument"),
                    _ => new Exception("Test exception")
                };
            }
            
            return Unit.Default;
        }
    }

    public class TestAlertSender : IAlertSender
    {
        public List<AlertContext> AlertsSent { get; } = new();

        public Task SendAlertAsync(AlertContext context, CancellationToken cancellationToken)
        {
            AlertsSent.Add(context);
            return Task.CompletedTask;
        }

        public void Reset()
        {
            AlertsSent.Clear();
        }
    }
}

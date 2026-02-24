using ChainSharp.Effect.Provider.Alerting.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace ChainSharp.Tests.Effect.Provider.Alerting.Analyzers;

/// <summary>
/// Tests for the ChainSharp alerting analyzers (ALERT001 and ALERT002).
/// </summary>
[TestFixture]
public class AlertingAnalyzerTests
{
    private const string StubTypes = @"
namespace ChainSharp.Effect.Models.Metadata
{
    public class Metadata { }
}

namespace ChainSharp.Effect.Provider.Alerting.Models
{
    public class AlertConfiguration { }
    
    public class AlertConfigurationBuilder
    {
        public static AlertConfigurationBuilder Create() => new();
        public AlertConfigurationBuilder WithinTimeSpan(System.TimeSpan window) => this;
        public AlertConfigurationBuilder MinimumFailures(int count) => this;
        public AlertConfigurationBuilder AlertOnEveryFailure() => this;
        public AlertConfigurationBuilder WhereExceptionType<T>() where T : System.Exception => this;
        public AlertConfigurationBuilder WhereFailureStepNameEquals(string name) => this;
        public AlertConfigurationBuilder WhereFailureStepName(System.Func<string, bool> predicate) => this;
        public AlertConfigurationBuilder AndCustomFilter(System.Func<ChainSharp.Effect.Models.Metadata.Metadata, bool> filter) => this;
        public AlertConfiguration Build() => new();
    }

    public class AlertingOptionsBuilder
    {
        public AlertingOptionsBuilder AddAlertSender<T>() where T : class => this;
        public AlertingOptionsBuilder WithDebouncing(System.TimeSpan period) => this;
    }
}

namespace ChainSharp.Effect.Configuration.ChainSharpEffectBuilder
{
    public class ChainSharpEffectConfigurationBuilder
    {
        public ChainSharpEffectConfigurationBuilder UseAlertingEffect(
            System.Action<ChainSharp.Effect.Provider.Alerting.Models.AlertingOptionsBuilder> configure) => this;
    }
}
";

    // ═══════════════════════════════════════════════════════════════════
    // ALERT001 Tests: AlertConfiguration requires TimeWindow and MinimumFailures
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task ALERT001_BothFieldsSet_NoDiagnostics()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .WithinTimeSpan(System.TimeSpan.FromHours(1))
            .MinimumFailures(3)
            .Build();
    }
}";

        var test = CreateAlertConfigTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_AlertOnEveryFailure_NoDiagnostics()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .AlertOnEveryFailure()
            .Build();
    }
}";

        var test = CreateAlertConfigTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_MissingTimeWindow_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .MinimumFailures(3)
            .{|#0:Build()|};
    }
}";

        var expected = new DiagnosticResult("ALERT001", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertConfigTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_MissingMinimumFailures_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .WithinTimeSpan(System.TimeSpan.FromHours(1))
            .{|#0:Build()|};
    }
}";

        var expected = new DiagnosticResult("ALERT001", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertConfigTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_MissingBothFields_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .WhereExceptionType<System.TimeoutException>()
            .{|#0:Build()|};
    }
}";

        var expected = new DiagnosticResult("ALERT001", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertConfigTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_WithFiltersButNoRequiredFields_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .WhereExceptionType<System.Exception>()
            .WhereFailureStepNameEquals(""Step"")
            .AndCustomFilter(m => true)
            .{|#0:Build()|};
    }
}";

        var expected = new DiagnosticResult("ALERT001", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertConfigTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT001_AlertOnEveryFailureWithFilters_NoDiagnostics()
    {
        var source = StubTypes + @"
class Test
{
    void M()
    {
        var config = ChainSharp.Effect.Provider.Alerting.Models.AlertConfigurationBuilder.Create()
            .AlertOnEveryFailure()
            .WhereExceptionType<System.TimeoutException>()
            .WhereFailureStepName(s => s.StartsWith(""DB""))
            .Build();
    }
}";

        var test = CreateAlertConfigTest(source);
        await test.RunAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ALERT002 Tests: UseAlertingEffect requires at least one alert sender
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task ALERT002_HasAlertSender_NoDiagnostics()
    {
        var source = StubTypes + @"
class TestSender { }

class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.UseAlertingEffect(alertOptions =>
            alertOptions.AddAlertSender<TestSender>());
    }
}";

        var test = CreateAlertOptionsTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT002_MultipleAlertSenders_NoDiagnostics()
    {
        var source = StubTypes + @"
class SnsSender { }
class EmailSender { }

class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.UseAlertingEffect(alertOptions =>
            alertOptions
                .AddAlertSender<SnsSender>()
                .AddAlertSender<EmailSender>());
    }
}";

        var test = CreateAlertOptionsTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT002_HasAlertSenderWithDebouncing_NoDiagnostics()
    {
        var source = StubTypes + @"
class TestSender { }

class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.UseAlertingEffect(alertOptions =>
            alertOptions
                .AddAlertSender<TestSender>()
                .WithDebouncing(System.TimeSpan.FromMinutes(15)));
    }
}";

        var test = CreateAlertOptionsTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT002_EmptyLambda_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.{|#0:UseAlertingEffect(alertOptions => { })|};
    }
}";

        var expected = new DiagnosticResult("ALERT002", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertOptionsTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT002_OnlyDebouncing_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.{|#0:UseAlertingEffect(alertOptions =>
            alertOptions.WithDebouncing(System.TimeSpan.FromMinutes(15)))|};
    }
}";

        var expected = new DiagnosticResult("ALERT002", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertOptionsTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ALERT002_NoCallsInLambda_ReportsDiagnostic()
    {
        var source = StubTypes + @"
class Test
{
    void M(ChainSharp.Effect.Configuration.ChainSharpEffectBuilder.ChainSharpEffectConfigurationBuilder builder)
    {
        builder.{|#0:UseAlertingEffect(_ => { })|};
    }
}";

        var expected = new DiagnosticResult("ALERT002", DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = CreateAlertOptionsTest(source, expected);
        await test.RunAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════

    private static CSharpAnalyzerTest<AlertConfigurationAnalyzer, DefaultVerifier> CreateAlertConfigTest(
        string testSource,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpAnalyzerTest<AlertConfigurationAnalyzer, DefaultVerifier>
        {
            TestCode = testSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    private static CSharpAnalyzerTest<AlertingOptionsAnalyzer, DefaultVerifier> CreateAlertOptionsTest(
        string testSource,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpAnalyzerTest<AlertingOptionsAnalyzer, DefaultVerifier>
        {
            TestCode = testSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }
}

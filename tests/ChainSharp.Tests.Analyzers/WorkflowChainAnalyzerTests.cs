using ChainSharp.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace ChainSharp.Tests.Analyzers;

/// <summary>
/// Tests for the WorkflowChainAnalyzer covering Phase 1 (basic chain validation)
/// and Phase 2 (tuple decomposition, interface resolution).
/// </summary>
[TestFixture]
public class WorkflowChainAnalyzerTests
{
    /// <summary>
    /// Minimal stub types that mirror ChainSharp's type structure.
    /// The analyzer matches by name + namespace, so these stubs are sufficient.
    /// </summary>
    private const string StubTypes =
        @"
namespace LanguageExt
{
    public struct Unit
    {
        public static readonly Unit Default = new Unit();
    }
}

namespace ChainSharp.Step
{
    public interface IStep<TIn, TOut> { }
}

namespace ChainSharp.Workflow
{
    public class Workflow<TInput, TReturn>
    {
        public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherInputs) => this;
        public Workflow<TInput, TReturn> Chain<TStep>() where TStep : class => this;
        public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>() where TStep : ChainSharp.Step.IStep<TIn, TOut> => this;
        public Workflow<TInput, TReturn> AddServices<T1>() => this;
        public Workflow<TInput, TReturn> AddServices<T1, T2>() => this;
        public Workflow<TInput, TReturn> IChain<TStep>() where TStep : class => this;
        public Workflow<TInput, TReturn> ShortCircuit<TStep>() where TStep : class => this;
        public Workflow<TInput, TReturn> Extract<TIn, TOut>() => this;
        public TReturn Resolve() => default!;
    }
}
";

    private static CSharpAnalyzerTest<WorkflowChainAnalyzer, DefaultVerifier> CreateTest(
        string testSource,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpAnalyzerTest<WorkflowChainAnalyzer, DefaultVerifier>
        {
            TestCode = testSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    // ──────────────────────────────────────────────
    // Phase 1: Basic chain validation
    // ──────────────────────────────────────────────

    [Test]
    public async Task BasicChain_TypesFlowCorrectly_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class OrderResult { }

    public class ProcessOrderStep : ChainSharp.Step.IStep<OrderRequest, OrderResult> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, OrderResult>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .Chain<ProcessOrderStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task MissingInputType_Reports_CHAIN001()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class Intermediate { }
    public class NeedsSpecial { }

    public class StepA : ChainSharp.Step.IStep<MyInput, Intermediate> { }
    public class StepB : ChainSharp.Step.IStep<NeedsSpecial, LanguageExt.Unit> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, LanguageExt.Unit>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .Chain<StepA>()
                .{|#0:Chain<StepB>()|}
                .Resolve();
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN001", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("StepB", "NeedsSpecial", "Unit, MyInput, Intermediate");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task MissingReturnType_Reports_CHAIN002()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class Receipt { }
    public class Validated { }

    public class ValidateStep : ChainSharp.Step.IStep<OrderRequest, Validated> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, Receipt>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .Chain<ValidateStep>()
                .{|#0:Resolve()|};
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN002", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Receipt", "Unit, OrderRequest, Validated");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Phase 2: Tuple validation
    // ──────────────────────────────────────────────

    [Test]
    public async Task TupleInput_AllComponentsPresent_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class User { }
    public class Order { }
    public class Combined { }

    public class ProduceUserStep : ChainSharp.Step.IStep<string, User> { }
    public class ProduceOrderStep : ChainSharp.Step.IStep<User, Order> { }
    public class CombineStep : ChainSharp.Step.IStep<(User, Order), Combined> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, Combined>
    {
        public void Run(string input)
        {
            Activate(input)
                .Chain<ProduceUserStep>()
                .Chain<ProduceOrderStep>()
                .Chain<CombineStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task TupleInput_ComponentMissing_Reports_CHAIN001()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class User { }
    public class Order { }
    public class Combined { }

    public class ProduceUserStep : ChainSharp.Step.IStep<MyInput, User> { }
    // Note: no step produces Order
    public class CombineStep : ChainSharp.Step.IStep<(User, Order), Combined> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Combined>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .Chain<ProduceUserStep>()
                .{|#0:Chain<CombineStep>()|}
                .Resolve();
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN001", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("CombineStep", "(User, Order)", "Unit, MyInput, User");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task TupleOutput_DecomposesComponents_AvailableDownstream()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class User { }
    public class Order { }

    public class ProducePairStep : ChainSharp.Step.IStep<string, (User, Order)> { }
    public class ConsumeUserStep : ChainSharp.Step.IStep<User, LanguageExt.Unit> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, LanguageExt.Unit>
    {
        public void Run(string input)
        {
            Activate(input)
                .Chain<ProducePairStep>()
                .Chain<ConsumeUserStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Phase 2: Interface resolution
    // ──────────────────────────────────────────────

    [Test]
    public async Task InterfaceInput_ConcreteImplementsInterface_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public interface IUser { }
    public class ConcreteUser : IUser { }
    public class Result { }

    public class ProduceUserStep : ChainSharp.Step.IStep<string, ConcreteUser> { }
    public class ConsumeInterfaceStep : ChainSharp.Step.IStep<IUser, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, Result>
    {
        public void Run(string input)
        {
            Activate(input)
                .Chain<ProduceUserStep>()
                .Chain<ConsumeInterfaceStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task InterfaceInput_NoImplementor_Reports_CHAIN001()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public interface IUser { }
    public class MyInput { }
    public class UnrelatedType { }
    public class Result { }

    public class ProduceUnrelatedStep : ChainSharp.Step.IStep<MyInput, UnrelatedType> { }
    public class ConsumeInterfaceStep : ChainSharp.Step.IStep<IUser, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Result>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .Chain<ProduceUnrelatedStep>()
                .{|#0:Chain<ConsumeInterfaceStep>()|}
                .Resolve();
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN001", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ConsumeInterfaceStep", "IUser", "Unit, MyInput, UnrelatedType");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Phase 2: Tuple return type validation
    // ──────────────────────────────────────────────

    [Test]
    public async Task TupleReturnType_AllComponentsPresent_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class User { }
    public class Order { }

    public class ProduceUserStep : ChainSharp.Step.IStep<string, User> { }
    public class ProduceOrderStep : ChainSharp.Step.IStep<User, Order> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, (User, Order)>
    {
        public void Run(string input)
        {
            Activate(input)
                .Chain<ProduceUserStep>()
                .Chain<ProduceOrderStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task TupleReturnType_ComponentMissing_Reports_CHAIN002()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class User { }
    public class Order { }

    public class ProduceUserStep : ChainSharp.Step.IStep<MyInput, User> { }
    // Note: no step produces Order

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, (User, Order)>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .Chain<ProduceUserStep>()
                .{|#0:Resolve()|};
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN002", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("(User, Order)", "Unit, MyInput, User");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Additional method tracking
    // ──────────────────────────────────────────────

    [Test]
    public async Task AddServices_TypesAvailableDownstream_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public interface IRepository { }
    public class Result { }

    public class ConsumeRepoStep : ChainSharp.Step.IStep<IRepository, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, Result>
    {
        public void Run(string input)
        {
            Activate(input)
                .AddServices<IRepository>()
                .Chain<ConsumeRepoStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task IChain_TracksTOut_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public interface IProduceResult : ChainSharp.Step.IStep<string, Result> { }
    public class Result { }

    public class ConsumeResultStep : ChainSharp.Step.IStep<Result, LanguageExt.Unit> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, LanguageExt.Unit>
    {
        public void Run(string input)
        {
            Activate(input)
                .IChain<IProduceResult>()
                .Chain<ConsumeResultStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task Extract_TracksTOut_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class Container { }
    public class Inner { }

    public class ProduceContainerStep : ChainSharp.Step.IStep<string, Container> { }
    public class ConsumeInnerStep : ChainSharp.Step.IStep<Inner, LanguageExt.Unit> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, LanguageExt.Unit>
    {
        public void Run(string input)
        {
            Activate(input)
                .Chain<ProduceContainerStep>()
                .Extract<Container, Inner>()
                .Chain<ConsumeInnerStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Phase 3: ShortCircuit tracking
    // ──────────────────────────────────────────────

    [Test]
    public async Task ShortCircuit_TypesFlowCorrectly_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class OrderResult { }

    public class ProcessOrderStep : ChainSharp.Step.IStep<OrderRequest, OrderResult> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, OrderResult>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .ShortCircuit<ProcessOrderStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ShortCircuit_MissingInputType_Reports_CHAIN001()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class NeedsSpecial { }
    public class Result { }

    public class BadStep : ChainSharp.Step.IStep<NeedsSpecial, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Result>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .{|#0:ShortCircuit<BadStep>()|}
                .Resolve();
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN001", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("BadStep", "NeedsSpecial", "Unit, MyInput");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ShortCircuit_ProvidesTReturn_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class Validated { }
    public class Receipt { }

    public class ValidateStep : ChainSharp.Step.IStep<OrderRequest, Validated> { }
    public class CacheCheckStep : ChainSharp.Step.IStep<OrderRequest, Receipt> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, Receipt>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .ShortCircuit<CacheCheckStep>()
                .Chain<ValidateStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ShortCircuit_DoesNotProvideTReturn_Reports_CHAIN002()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class Intermediate { }
    public class Receipt { }

    public class MissStep : ChainSharp.Step.IStep<OrderRequest, Intermediate> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, Receipt>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .ShortCircuit<MissStep>()
                .{|#0:Resolve()|};
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN002", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Receipt", "Unit, OrderRequest, Intermediate");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }

    [Test]
    public async Task ShortCircuit_TOutAvailableForDownstreamChain_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class OrderRequest { }
    public class CachedData { }
    public class FinalResult { }

    public class CacheStep : ChainSharp.Step.IStep<OrderRequest, CachedData> { }
    public class ProcessStep : ChainSharp.Step.IStep<CachedData, FinalResult> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<OrderRequest, FinalResult>
    {
        public void Run(OrderRequest input)
        {
            Activate(input)
                .ShortCircuit<CacheStep>()
                .Chain<ProcessStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task ShortCircuit_WithTupleOutput_DecomposesComponents()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class User { }
    public class Order { }

    public class ProducePairStep : ChainSharp.Step.IStep<string, (User, Order)> { }
    public class ConsumeUserStep : ChainSharp.Step.IStep<User, LanguageExt.Unit> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<string, LanguageExt.Unit>
    {
        public void Run(string input)
        {
            Activate(input)
                .ShortCircuit<ProducePairStep>()
                .Chain<ConsumeUserStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    // ──────────────────────────────────────────────
    // Activate other inputs
    // ──────────────────────────────────────────────

    [Test]
    public async Task Activate_OtherInputs_TypeAvailableForResolve_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, TestWorkflow>
    {
        public void Run(MyInput input)
        {
            Activate(input, this)
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task Activate_OtherInputs_SatisfiesStepInput_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class ExtraService { }
    public class Result { }

    public class NeedsServiceStep : ChainSharp.Step.IStep<ExtraService, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Result>
    {
        public void Run(MyInput input, ExtraService svc)
        {
            Activate(input, svc)
                .Chain<NeedsServiceStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task Activate_OtherInputs_InterfacesTracked_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public interface IService { }
    public class ConcreteService : IService { }
    public class Result { }

    public class NeedsInterfaceStep : ChainSharp.Step.IStep<IService, Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Result>
    {
        public void Run(MyInput input, ConcreteService svc)
        {
            Activate(input, svc)
                .Chain<NeedsInterfaceStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task Activate_MultipleOtherInputs_AllTracked_NoDiagnostics()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class ServiceA { }
    public class ServiceB { }
    public class Result { }

    public class NeedsBothStep : ChainSharp.Step.IStep<(ServiceA, ServiceB), Result> { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, Result>
    {
        public void Run(MyInput input, ServiceA a, ServiceB b)
        {
            Activate(input, a, b)
                .Chain<NeedsBothStep>()
                .Resolve();
        }
    }
}";

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Test]
    public async Task Activate_NoOtherInputs_MissingType_StillReports_CHAIN002()
    {
        var source =
            StubTypes
            + @"
namespace TestApp
{
    public class MyInput { }
    public class MissingType { }

    public class TestWorkflow : ChainSharp.Workflow.Workflow<MyInput, MissingType>
    {
        public void Run(MyInput input)
        {
            Activate(input)
                .{|#0:Resolve()|};
        }
    }
}";

        var expected = new DiagnosticResult("CHAIN002", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("MissingType", "Unit, MyInput");

        var test = CreateTest(source, expected);
        await test.RunAsync();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ChainSharp Scheduler × Flowthru Spaceflights Sample
//
// Demonstrates ChainSharp's scheduler orchestrating three Flowthru data
// pipelines as a linear dependency chain:
//
//   data-processing → data-science → reporting
//
// Pipeline logic, data catalog, and example datasets are from the
// KedroSpaceflights.Pure example in the Flowthru project by @Spelkington:
//   https://github.com/chaoticgoodcomputing/flowthru
//
// Original dataset: Kedro Spaceflights tutorial (Apache 2.0)
//   https://github.com/kedro-org/kedro-starters
// ─────────────────────────────────────────────────────────────────────────────

using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.StepProvider.Progress.Extensions;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting;
using KedroSpaceflights.Pure.Data;
using KedroSpaceflights.Pure.Pipelines.DataProcessing;
using KedroSpaceflights.Pure.Pipelines.DataScience;
using KedroSpaceflights.Pure.Pipelines.Reporting;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("ChainSharpDatabase")
    ?? throw new InvalidOperationException("Connection string 'ChainSharpDatabase' not found.");

builder.Services.AddLogging(logging => logging.AddConsole());
builder.AddChainSharpDashboard();

// ── Register Flowthru services ──────────────────────────────────────────────
// Pipeline logic by @Spelkington — https://github.com/chaoticgoodcomputing/flowthru
builder.Services.AddFlowthru(flowthru =>
{
    flowthru.UseConfiguration();
    flowthru.UseCatalog(_ => new Catalog("Data"));

    flowthru
        .RegisterPipeline<Catalog>(label: "DataProcessing", pipeline: DataProcessingPipeline.Create)
        .WithDescription("Preprocesses companies and shuttles data");

    flowthru
        .RegisterPipelineWithConfiguration<Catalog, DataSciencePipeline.Params>(
            label: "DataScience",
            pipeline: DataSciencePipeline.Create,
            configurationSection: "Flowthru:Pipelines:DataScience"
        )
        .WithDescription("Trains linear regression model for price prediction");

    flowthru
        .RegisterPipelineWithConfiguration<Catalog, ReportingPipeline.Params>(
            label: "Reporting",
            pipeline: ReportingPipeline.Create,
            configurationSection: "Flowthru:Pipelines:Reporting"
        )
        .WithDescription("Generates passenger capacity reports and visualizations");
});

// ── Register ChainSharp Effect + Scheduler ──────────────────────────────────
builder.Services.AddChainSharpEffects(
    options =>
        options
            .AddEffectWorkflowBus(
                assemblies: [typeof(Program).Assembly, typeof(ManifestManagerWorkflow).Assembly]
            )
            .AddPostgresEffect(connectionString)
            .AddJsonEffect()
            .SaveWorkflowParameters()
            .AddStepProgress()
            .AddScheduler(scheduler =>
            {
                scheduler
                    .AddMetadataCleanup(cleanup =>
                    {
                        cleanup.AddWorkflowType<IDataProcessingPipelineWorkflow>();
                        cleanup.AddWorkflowType<IDataSciencePipelineWorkflow>();
                        cleanup.AddWorkflowType<IReportingPipelineWorkflow>();
                    })
                    .JobDispatcherPollingInterval(TimeSpan.FromSeconds(2))
                    .UsePostgresTaskServer();

                // ── Spaceflights Pipeline Topology ──────────────────────────────
                //    data-processing (root, every 5 min)
                //      └── data-science   (ThenInclude — depends on data-processing)
                //          └── reporting   (ThenInclude — depends on data-science)
                scheduler
                    .Schedule<IDataProcessingPipelineWorkflow>(
                        "data-processing",
                        new DataProcessingPipelineInput(),
                        Every.Minutes(5)
                    )
                    .ThenInclude<IDataSciencePipelineWorkflow>(
                        "data-science",
                        new DataSciencePipelineInput()
                    )
                    .ThenInclude<IReportingPipelineWorkflow>(
                        "reporting",
                        new ReportingPipelineInput()
                    );
            })
);

var app = builder.Build();

app.UseChainSharpDashboard();

app.Run();

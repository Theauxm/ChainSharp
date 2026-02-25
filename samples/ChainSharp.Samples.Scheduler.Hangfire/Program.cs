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
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlwaysFails;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.DataQualityCheck;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.GoodbyeWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.TransformLoad;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("ChainSharpDatabase")
    ?? throw new InvalidOperationException("Connection string 'ChainSharpDatabase' not found.");

builder.Services.AddLogging(logging => logging.AddConsole());
builder.AddChainSharpDashboard();

builder.Services.AddChainSharpEffects(options =>
    options
        .AddEffectWorkflowBus(assemblies:
        [
            typeof(Program).Assembly,
            typeof(ManifestManagerWorkflow).Assembly,
        ])
        .AddPostgresEffect(connectionString)
        .AddJsonEffect()
        .SaveWorkflowParameters()
        .AddStepProgress()
        .AddScheduler(scheduler =>
        {
            // ── Global Configuration ──────────────────────────────────────────────
            scheduler
                .AddMetadataCleanup(cleanup =>
                {
                    cleanup.AddWorkflowType<IHelloWorldWorkflow>();
                    cleanup.AddWorkflowType<IGoodbyeWorldWorkflow>();
                    cleanup.AddWorkflowType<IExtractImportWorkflow>();
                    cleanup.AddWorkflowType<ITransformLoadWorkflow>();
                    cleanup.AddWorkflowType<IDataQualityCheckWorkflow>();
                    cleanup.AddWorkflowType<IAlwaysFailsWorkflow>();
                })
                .JobDispatcherPollingInterval(TimeSpan.FromSeconds(2))
                .UsePostgresTaskServer();

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 1. SIMPLE RECURRING SCHEDULE
            //    Schedule() registers a single workflow on a recurring timer.
            //    Every.* helpers create interval-based schedules.
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler.Schedule<IHelloWorldWorkflow>(
                "hello-world",
                new HelloWorldInput { Name = "ChainSharp" },
                Every.Seconds(20)
            );

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 2. CRON-BASED SCHEDULE
            //    Cron.* helpers: Minutely(), Hourly(), Daily(), Weekly(),
            //    Monthly(), Expression("0 */6 * * *")
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler.Schedule<IGoodbyeWorldWorkflow>(
                "goodbye-nightly",
                new GoodbyeWorldInput { Name = "Night Shift" },
                Cron.Daily(hour: 3)
            );

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 3. RETRY POLICY & DEAD LETTERS
            //    MaxRetries(N) limits retry attempts before dead-lettering.
            //    This workflow always throws, so it dead-letters after 1 attempt.
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler.Schedule<IAlwaysFailsWorkflow>(
                "always-fails",
                new AlwaysFailsInput { Scenario = "Database connection timeout" },
                Every.Seconds(30),
                o => o.MaxRetries(1)
            );

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 4. DEPENDENCY TOPOLOGY
            //    Include()      → fan-out from the root Schedule
            //    ThenInclude()  → chain from the most recently declared manifest
            //    IncludeMany()  → batch fan-out from the root (no DependsOn needed)
            //
            //    hello-greeter (root, every 2 min)
            //      ├── farewell-a      (Include  — depends on root)
            //      ├── farewell-b      (Include  — depends on root)
            //      │   └── farewell-c  (ThenInclude — depends on farewell-b)
            //      └── broadcast-{0…4} (IncludeMany — all depend on root)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler
                .Schedule<IHelloWorldWorkflow>(
                    "hello-greeter",
                    new HelloWorldInput { Name = "Greeter Pipeline" },
                    Every.Minutes(2)
                )
                .Include<IGoodbyeWorldWorkflow>(
                    "farewell-a",
                    new GoodbyeWorldInput { Name = "Branch A" }
                )
                .Include<IGoodbyeWorldWorkflow>(
                    "farewell-b",
                    new GoodbyeWorldInput { Name = "Branch B" }
                )
                .ThenInclude<IGoodbyeWorldWorkflow>(
                    "farewell-c",
                    new GoodbyeWorldInput { Name = "Chained after B" }
                )
                .IncludeMany<IGoodbyeWorldWorkflow>(
                    "broadcast",
                    Enumerable
                        .Range(0, 5)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new GoodbyeWorldInput { Name = $"Recipient {i}" }
                            ))
                );

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 5. ETL PIPELINE: ScheduleMany → IncludeMany → ThenIncludeMany
            //    ScheduleMany()     — registers N manifests in one transaction.
            //                         Name-based IDs: "{name}-{item.Id}".
            //    IncludeMany()      — batch dependents with explicit DependsOn per item.
            //    ThenIncludeMany()  — batch chain from the previous IncludeMany cursor.
            //
            //    extract-customer-{i} (every 5 min)
            //      └── transform-customer-{i} (IncludeMany, DependsOn each extract)
            //          └── dq-customer-{i}    (ThenIncludeMany, DependsOn each transform)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler
                .ScheduleMany<IExtractImportWorkflow>(
                    "extract-customer",
                    Enumerable
                        .Range(0, 10)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new ExtractImportInput { TableName = "Customer", Index = i }
                            )),
                    Every.Minutes(5)
                )
                .IncludeMany<ITransformLoadWorkflow>(
                    "transform-customer",
                    Enumerable
                        .Range(0, 10)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new TransformLoadInput { TableName = "Customer", Index = i },
                                DependsOn: $"extract-customer-{i}"
                            ))
                )
                .ThenIncludeMany<IDataQualityCheckWorkflow>(
                    "dq-customer",
                    Enumerable
                        .Range(0, 10)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new DataQualityCheckInput
                                {
                                    TableName = "Customer",
                                    Index = i,
                                    AnomalyCount = 0,
                                },
                                DependsOn: $"transform-customer-{i}"
                            ))
                );

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 6. DORMANT DEPENDENTS & GROUP CONCURRENCY
            //    Dormant()             — dependents that never auto-fire; activated
            //                            at runtime via IDormantDependentContext.
            //    Priority(N)           — dispatch order (0–31, higher = first).
            //    Group(MaxActiveJobs)  — caps concurrent jobs within the batch.
            //
            //    extract-transaction-{i} (every 5 min, priority 24, max 10 concurrent)
            //      └── dq-transaction-{i} (Dormant — activated only when anomalies found)
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            scheduler
                .ScheduleMany<IExtractImportWorkflow>(
                    "extract-transaction",
                    Enumerable
                        .Range(0, 30)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new ExtractImportInput { TableName = "Transaction", Index = i }
                            )),
                    Every.Minutes(5),
                    options => options.Priority(24).Group(group => group.MaxActiveJobs(10))
                )
                .IncludeMany<IDataQualityCheckWorkflow>(
                    "dq-transaction",
                    Enumerable
                        .Range(0, 30)
                        .Select(i =>
                            new ManifestItem(
                                $"{i}",
                                new DataQualityCheckInput
                                {
                                    TableName = "Transaction",
                                    Index = i,
                                    AnomalyCount = 0,
                                },
                                DependsOn: $"extract-transaction-{i}"
                            )),
                    options: o => o.Dormant()
                );
        })
);

var app = builder.Build();

app.UseChainSharpDashboard();

app.Run();

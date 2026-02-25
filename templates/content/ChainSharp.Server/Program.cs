using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Server.Workflows.HelloWorld;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("ChainSharpDatabase")
    ?? throw new InvalidOperationException("Connection string 'ChainSharpDatabase' not found.");

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.AddChainSharpDashboard();

builder.Services.AddChainSharpEffects(
    options =>
        options
            .AddEffectWorkflowBus(
                assemblies: [typeof(Program).Assembly, typeof(ManifestManagerWorkflow).Assembly,]
            )
            .AddPostgresEffect(connectionString)
            .AddJsonEffect()
            .SaveWorkflowParameters()
            .AddScheduler(scheduler =>
            {
                scheduler
                    .AddMetadataCleanup(cleanup =>
                    {
                        cleanup.AddWorkflowType<IHelloWorldWorkflow>();
                    })
                    .UseHangfire(connectionString)
                    .Schedule<IHelloWorldWorkflow>(
                        "hello-world",
                        new HelloWorldInput { Name = "ChainSharp" },
                        Every.Seconds(20)
                    );
            })
);

var app = builder.Build();

app.UseChainSharpDashboard();
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

app.Run();

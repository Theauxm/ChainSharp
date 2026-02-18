---
layout: default
title: AddScheduler
parent: Scheduler API
grand_parent: API Reference
nav_order: 1
---

# AddScheduler

Adds the ChainSharp scheduler subsystem. Registers `IManifestScheduler`, the background polling service, and all scheduler infrastructure. Provides a `SchedulerConfigurationBuilder` lambda for configuring global options, task servers, and startup schedules.

## Signature

```csharp
public static ChainSharpEffectConfigurationBuilder AddScheduler(
    this ChainSharpEffectConfigurationBuilder builder,
    Action<SchedulerConfigurationBuilder> configure
)
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `configure` | `Action<SchedulerConfigurationBuilder>` | Yes | Lambda that receives the scheduler builder for configuring options, task servers, and schedules |

## Returns

`ChainSharpEffectConfigurationBuilder` — the parent builder, for continued fluent chaining.

## Example

```csharp
services.AddChainSharpEffects(options => options
    .AddPostgresEffect(connectionString)
    .AddEffectWorkflowBus(assemblies: typeof(Program).Assembly)
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .PollingInterval(TimeSpan.FromSeconds(30))
        .MaxActiveJobs(50)
        .DefaultMaxRetries(5)
        .DefaultRetryDelay(TimeSpan.FromMinutes(2))
        .RetryBackoffMultiplier(2.0)
        .MaxRetryDelay(TimeSpan.FromHours(1))
        .DefaultJobTimeout(TimeSpan.FromMinutes(20))
        .RecoverStuckJobsOnStartup()
        .AddMetadataCleanup()
        .Schedule<IMyWorkflow, MyInput>(
            "my-job",
            new MyInput(),
            Every.Minutes(5))
    )
);
```

## SchedulerConfigurationBuilder Options

These methods are available on the `SchedulerConfigurationBuilder` passed to the `configure` lambda:

### Task Server

| Method | Description |
|--------|-------------|
| [UseHangfire]({% link api-reference/scheduler-api/use-hangfire.md %}) | Configures Hangfire with PostgreSQL as the task server |
| `UseInMemoryTaskServer()` | Uses a synchronous in-memory task server (for testing) |
| `UseTaskServer(Action<IServiceCollection>)` | Registers a custom task server implementation |

### Global Options

| Method | Parameter | Default | Description |
|--------|-----------|---------|-------------|
| `PollingInterval(TimeSpan)` | interval | 60 seconds | How often ManifestManager polls for pending jobs |
| `MaxActiveJobs(int?)` | maxJobs | 100 | Max concurrent active jobs (Pending + InProgress). `null` = unlimited |
| `ExcludeFromMaxActiveJobs<TWorkflow>()` | — | — | Excludes a workflow type from the MaxActiveJobs count |
| `DefaultMaxRetries(int)` | maxRetries | 3 | Retry attempts before dead-lettering |
| `DefaultRetryDelay(TimeSpan)` | delay | 5 minutes | Base delay between retries |
| `RetryBackoffMultiplier(double)` | multiplier | 2.0 | Exponential backoff multiplier. Set to 1.0 for constant delay |
| `MaxRetryDelay(TimeSpan)` | maxDelay | 1 hour | Caps retry delay to prevent unbounded growth |
| `DefaultJobTimeout(TimeSpan)` | timeout | 1 hour | Timeout after which a running job is considered stuck |
| `RecoverStuckJobsOnStartup(bool)` | recover | `true` | Whether to auto-recover stuck jobs on startup |

### Startup Schedules

| Method | Description |
|--------|-------------|
| [Schedule]({% link api-reference/scheduler-api/schedule.md %}) | Schedules a single recurring workflow (seeded on startup) |
| [ScheduleMany]({% link api-reference/scheduler-api/schedule-many.md %}) | Batch-schedules manifests from a collection |
| [Then / ThenMany]({% link api-reference/scheduler-api/dependent-scheduling.md %}) | Schedules dependent workflows |
| [AddMetadataCleanup]({% link api-reference/scheduler-api/add-metadata-cleanup.md %}) | Enables automatic metadata purging |

## Remarks

- `AddScheduler` requires a data provider (`AddPostgresEffect` or `AddInMemoryEffect`) and `AddEffectWorkflowBus` to be configured first.
- Internal scheduler workflows (`ManifestManager`, `JobDispatcher`, `TaskServerExecutor`, `MetadataCleanup`) are automatically excluded from `MaxActiveJobs`.
- Manifests declared via `Schedule`/`ScheduleMany` are not created immediately — they are seeded on application startup by the `ManifestPollingService`.

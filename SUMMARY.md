# ManifestScheduler API Design Summary

This document outlines the proposed API for simplifying manifest creation in ChainSharp.Effect.Scheduler. The goal is to eliminate the manual, error-prone process of creating manifests and replace it with a clean, type-safe API inspired by Hangfire's patterns.

---

## Problem Statement

Currently, creating a manifest requires extensive boilerplate:

```csharp
var manifest = new Manifest
{
    ExternalId = "sample-hello-world",
    Name = typeof(IHelloWorldWorkflow).AssemblyQualifiedName!,              // Manual assembly name
    PropertyTypeName = typeof(HelloWorldInput).AssemblyQualifiedName,       // Manual assembly name
    Properties = System.Text.Json.JsonSerializer.Serialize(new HelloWorldInput  // Manual serialization
    {
        Name = "ChainSharp Scheduler"
    }),
    IsEnabled = true,
    ScheduleType = ScheduleType.Interval,
    IntervalSeconds = 60,
    MaxRetries = 3
};

context.Manifests.Add(manifest);
await context.SaveChanges(ct);
```

**Issues:**
1. Users must know about `AssemblyQualifiedName` and use it correctly
2. Manual JSON serialization of properties
3. No compile-time safety between workflow type and input type
4. Verbose and repetitive for bulk job creation
5. Easy to make mistakes that only surface at runtime

---

## Proposed Solution

### Core API: `IManifestScheduler`

A simple interface with two scheduling methods plus management operations:

```csharp
public interface IManifestScheduler
{
    /// <summary>
    /// Schedules a single workflow to run on a recurring basis.
    /// Creates or updates the manifest by ExternalId.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type. Must implement IEffectWorkflow&lt;TInput, TOutput&gt;
    /// for some TOutput. The scheduler resolves the workflow via WorkflowBus using the input type.</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        CancellationToken ct = default)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties;

    /// <summary>
    /// Schedules multiple instances of a workflow from a collection.
    /// Creates or updates manifests by ExternalId in a single transaction.
    /// If any manifest fails to save, the entire batch is rolled back.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type. Must implement IEffectWorkflow&lt;TInput, TOutput&gt;
    /// for some TOutput. The scheduler resolves the workflow via WorkflowBus using the input type.</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The source collection element type</typeparam>
    Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        CancellationToken ct = default)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties;
    
    /// <summary>
    /// Disables a scheduled job (keeps the manifest, stops execution).
    /// </summary>
    Task DisableAsync(string externalId, CancellationToken ct = default);
    
    /// <summary>
    /// Enables a previously disabled job.
    /// </summary>
    Task EnableAsync(string externalId, CancellationToken ct = default);
    
    /// <summary>
    /// Triggers immediate execution of a scheduled job.
    /// </summary>
    Task TriggerAsync(string externalId, CancellationToken ct = default);
}
```

### Schedule Definition

A `Schedule` record with readable helper classes:

```csharp
public record Schedule
{
    public ScheduleType Type { get; init; }
    public TimeSpan? Interval { get; init; }
    public string? CronExpression { get; init; }
    
    public static Schedule FromInterval(TimeSpan interval) 
        => new() { Type = ScheduleType.Interval, Interval = interval };
    
    public static Schedule FromCron(string expression) 
        => new() { Type = ScheduleType.Cron, CronExpression = expression };
}
```

**`Every` helper for interval-based schedules:**

```csharp
public static class Every
{
    public static Schedule Seconds(int seconds) 
        => Schedule.FromInterval(TimeSpan.FromSeconds(seconds));
    
    public static Schedule Minutes(int minutes) 
        => Schedule.FromInterval(TimeSpan.FromMinutes(minutes));
    
    public static Schedule Hours(int hours) 
        => Schedule.FromInterval(TimeSpan.FromHours(hours));
    
    public static Schedule Days(int days) 
        => Schedule.FromInterval(TimeSpan.FromDays(days));
}
```

**`Cron` helper for cron-based schedules:**

```csharp
public static class Cron
{
    public static Schedule Minutely() 
        => Schedule.FromCron("* * * * *");
    
    public static Schedule Hourly(int minute = 0) 
        => Schedule.FromCron($"{minute} * * * *");
    
    public static Schedule Daily(int hour = 0, int minute = 0) 
        => Schedule.FromCron($"{minute} {hour} * * *");
    
    public static Schedule Weekly(DayOfWeek day, int hour = 0, int minute = 0) 
        => Schedule.FromCron($"{minute} {hour} * * {(int)day}");
    
    public static Schedule Monthly(int day = 1, int hour = 0, int minute = 0) 
        => Schedule.FromCron($"{minute} {hour} {day} * *");
    
    public static Schedule Expression(string cronExpression) 
        => Schedule.FromCron(cronExpression);
}
```

### Manifest Options

Additional configuration for manifests:

```csharp
public class ManifestOptions
{
    /// <summary>
    /// Whether the manifest is enabled for scheduling. Defaults to true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Maximum retry attempts before dead-lettering. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Timeout for job execution. Null uses the global default.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
```

### IManifestProperties

Workflow inputs must implement `IManifestProperties` to be schedulable. This is a marker interface that signals the type is safe for serialization and storage:

```csharp
// Defined in ChainSharp.Effect.Models.Manifest
public interface IManifestProperties { }
```

**Why a marker interface?**

1. **Explicit opt-in**: Not all workflow inputs should be schedulable. The interface makes it a conscious choice.
2. **Compile-time safety**: `ScheduleAsync<TWorkflow, TInput>` constraints catch mismatches before runtime.
3. **Serialization contract**: Types implementing this interface must be JSON-serializable. The `Manifest.Properties` column stores the serialized input, and `ManifestExecutorWorkflow` deserializes it when the job runs.

```csharp
// ✅ Correct - implements IManifestProperties
public record SyncTableInput : IManifestProperties
{
    public string TableName { get; init; } = "";
    public int BatchSize { get; init; } = 1000;
}

// ❌ Won't compile - missing IManifestProperties
public record RegularInput { public string Value { get; init; } }
// scheduler.ScheduleAsync<ISyncWorkflow, RegularInput>(...) // Compile error!
```

---

## Usage Examples

### Single Job

```csharp
// Inject IManifestScheduler
public class JobSetupService(IManifestScheduler scheduler)
{
    public async Task SetupJobs()
    {
        // Simple interval-based job
        await scheduler.ScheduleAsync<IHelloWorldWorkflow, HelloWorldInput>(
            "hello-world",
            new HelloWorldInput { Name = "Scheduler" },
            Every.Minutes(1));
        
        // Cron-based job with options
        await scheduler.ScheduleAsync<IDailyReportWorkflow, DailyReportInput>(
            "daily-report",
            new DailyReportInput { ReportType = "sales" },
            Cron.Daily(hour: 3),
            opts => opts.MaxRetries = 5);
        
        // Complex cron expression
        await scheduler.ScheduleAsync<IWeeklyCleanupWorkflow, CleanupInput>(
            "weekly-cleanup",
            new CleanupInput { DaysToKeep = 30 },
            Cron.Expression("0 2 * * 0")); // Sundays at 2am
    }
}
```

### Bulk Jobs - Simple List

```csharp
var tables = new[] { "users", "orders", "products", "inventory" };

// All manifests are saved in a single transaction.
// If any fails, the entire batch rolls back.
await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    tableName => (
        ExternalId: $"sync-{tableName}",
        Input: new SyncTableInput { TableName = tableName }
    ),
    Every.Minutes(5));
```

### Bulk Jobs - Varying Configuration Per Item

```csharp
var tableConfigs = new[]
{
    (Name: "users", Interval: TimeSpan.FromMinutes(1), Retries: 5),
    (Name: "orders", Interval: TimeSpan.FromMinutes(1), Retries: 5),
    (Name: "products", Interval: TimeSpan.FromMinutes(15), Retries: 3),
    (Name: "logs", Interval: TimeSpan.FromHours(1), Retries: 1),
};

foreach (var config in tableConfigs)
{
    await scheduler.ScheduleAsync<ISyncTableWorkflow, SyncTableInput>(
        $"sync-{config.Name}",
        new SyncTableInput { TableName = config.Name },
        Schedule.FromInterval(config.Interval),
        opts => opts.MaxRetries = config.Retries);
}
```

### Bulk Jobs - Multi-Dimensional (Grid Pattern)

For cases where jobs are split across multiple dimensions (e.g., table × slice index):

```csharp
// Each table has a different number of slices
var tables = new[] 
{ 
    (Name: "customer", SliceCount: 100), 
    (Name: "partner", SliceCount: 10), 
    (Name: "user", SliceCount: 1000) 
};

// Option 1: Loop per table (each loop iteration is a separate transaction)
foreach (var (table, sliceCount) in tables)
{
    await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, int>(
        Enumerable.Range(0, sliceCount),
        slice => (
            ExternalId: $"sync-{table}-{slice}",
            Input: new SyncTableInput { TableName = table, SliceIndex = slice }
        ),
        Every.Minutes(5));
}

// Option 2: Flatten with LINQ (single transaction for all jobs)
var allJobs = tables.SelectMany(t => 
    Enumerable.Range(0, t.SliceCount).Select(slice => (t.Name, slice)));

await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, (string Table, int Slice)>(
    allJobs,
    item => (
        ExternalId: $"sync-{item.Table}-{item.Slice}",
        Input: new SyncTableInput { TableName = item.Table, SliceIndex = item.Slice }
    ),
    Every.Minutes(5));
```

### Management Operations

```csharp
// Disable a job (e.g., during maintenance)
await scheduler.DisableAsync("sync-users");

// Re-enable
await scheduler.EnableAsync("sync-users");

// Trigger immediate execution
await scheduler.TriggerAsync("sync-users");
```

---

## Startup Configuration

### Extension Methods for `SchedulerConfiguration`

Mirror the `IManifestScheduler` interface for use during service configuration:

```csharp
public static class SchedulerConfigurationExtensions
{
    public static SchedulerConfiguration Schedule<TWorkflow, TInput>(
        this SchedulerConfiguration config,
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        // Capture the generic types in a closure - no reflection needed at startup
        config.PendingManifests.Add(new PendingManifest
        {
            ExternalId = externalId,
            ScheduleFunc = (scheduler, ct) => 
                scheduler.ScheduleAsync<TWorkflow, TInput>(externalId, input, schedule, configure, ct)
        });
        
        return config;
    }

    public static SchedulerConfiguration ScheduleMany<TWorkflow, TInput, TSource>(
        this SchedulerConfiguration config,
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        // For ScheduleMany, we create a single pending manifest that handles the entire batch
        // The ExternalId here is just for logging - actual IDs come from the map function
        var firstId = sources.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";
        
        config.PendingManifests.Add(new PendingManifest
        {
            ExternalId = $"{firstId}... (batch)",
            ScheduleFunc = async (scheduler, ct) =>
            {
                var results = await scheduler.ScheduleManyAsync<TWorkflow, TInput, TSource>(
                    sources, map, schedule, configure, ct);
                return results.FirstOrDefault()!; // Return first for the Manifest return type
            }
        });
        
        return config;
    }
}
```

### Startup Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        typeof(Program).Assembly,
        typeof(ManifestExecutorWorkflow).Assembly)
    .AddPostgresEffect(connectionString)
    .AddScheduler(scheduler => scheduler
        .PollingInterval(TimeSpan.FromSeconds(30))
        .MaxJobsPerCycle(100)
        .DefaultMaxRetries(3)
        .UseHangfire(
            config => config.UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)),
            server => server.Queues = new[] { "default", "scheduler" })
        
        // Static job definitions - created/updated on startup
        .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
            "hello-world",
            new HelloWorldInput { Name = "ChainSharp Scheduler" },
            Every.Minutes(1))
        
        .Schedule<IDailyReportWorkflow, DailyReportInput>(
            "daily-report",
            new DailyReportInput { ReportType = "sales" },
            Cron.Daily(hour: 3))
        
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            new[] { "users", "orders", "products" },
            table => ($"sync-{table}", new SyncTableInput { TableName = table }),
            Every.Minutes(5))
    )
);

var app = builder.Build();

app.UseHangfireDashboard("/hangfire");
app.UseChainSharpScheduler(); // Seeds pending manifests on startup

app.Run();
```

---

## JSON Configuration (Optional)

For static manifests defined in configuration files.

> **⚠️ Security Warning**: JSON configuration uses `$type` discriminators to resolve input types.
> This requires trusted configuration sources. **Never load manifest JSON from untrusted user input**
> as it could lead to arbitrary type instantiation. Only use this feature with:
> - `appsettings.json` files deployed with your application
> - Secure configuration providers (Azure Key Vault, AWS Secrets Manager, etc.)
> - Environment variables from trusted infrastructure

### appsettings.json

```json
{
  "ChainSharp": {
    "Scheduler": {
      "Manifests": [
        {
          "Workflow": "MyApp.Workflows.ISyncTableWorkflow, MyApp",
          "ExternalId": "sync-users",
          "Input": {
            "$type": "MyApp.Inputs.SyncTableInput, MyApp",
            "TableName": "users"
          },
          "Interval": "00:05:00",
          "MaxRetries": 3
        },
        {
          "Workflow": "MyApp.Workflows.IDailyReportWorkflow, MyApp",
          "ExternalId": "daily-report",
          "Input": {
            "$type": "MyApp.Inputs.DailyReportInput, MyApp",
            "ReportType": "sales"
          },
          "Cron": "0 3 * * *",
          "MaxRetries": 5
        }
      ]
    }
  }
}
```

### Registration

```csharp
builder.Services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(/* ... */)
        .AddManifestsFromConfiguration(
            builder.Configuration.GetSection("ChainSharp:Scheduler:Manifests"))
    )
);
```

---

## Implementation Details

### Internal `PendingManifest` Class

Used by startup configuration to collect manifest definitions before the application starts. Uses a closure to capture the generic type information at configuration time, avoiding reflection during startup:

```csharp
internal class PendingManifest
{
    /// <summary>
    /// A closure that captures the generic type parameters and schedules the manifest.
    /// This avoids reflection when processing pending manifests during startup.
    /// </summary>
    public required Func<IManifestScheduler, CancellationToken, Task<Manifest>> ScheduleFunc { get; init; }
    
    /// <summary>
    /// The external ID for logging and debugging purposes.
    /// </summary>
    public required string ExternalId { get; init; }
}
```

### `SchedulerConfiguration` Changes

Add a collection to hold pending manifests:

```csharp
public class SchedulerConfiguration
{
    // Existing properties...
    
    internal List<PendingManifest> PendingManifests { get; } = new();
}
```

### `UseChainSharpScheduler` Changes

On startup, process pending manifests and upsert them. The closure pattern means no reflection is needed:

```csharp
public static IApplicationBuilder UseChainSharpScheduler(this IApplicationBuilder app)
{
    // Existing logic...
    
    // Seed pending manifests
    using var scope = app.ApplicationServices.CreateScope();
    var scheduler = scope.ServiceProvider.GetRequiredService<IManifestScheduler>();
    var config = scope.ServiceProvider.GetRequiredService<SchedulerConfiguration>();
    
    foreach (var pending in config.PendingManifests)
    {
        // The closure already has the generic types captured - just invoke it
        pending.ScheduleFunc(scheduler, CancellationToken.None).GetAwaiter().GetResult();
    }
    
    return app;
}
```

### `ManifestScheduler` Implementation

```csharp
public class ManifestScheduler(
    IDataContextProviderFactory dataContextFactory,
    IWorkflowRegistry workflowRegistry,
    ILogger<ManifestScheduler> logger) : IManifestScheduler
{
    public async Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        CancellationToken ct = default)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        // Validate workflow is registered - fail fast at scheduling time, not execution time
        var inputType = typeof(TInput);
        if (!workflowRegistry.InputTypeToWorkflow.ContainsKey(inputType))
        {
            throw new InvalidOperationException(
                $"Workflow for input type '{inputType.Name}' is not registered in the WorkflowRegistry. " +
                $"Ensure the workflow assembly is included in AddEffectWorkflowBus().");
        }
        
        var options = new ManifestOptions();
        configure?.Invoke(options);
        
        using var context = dataContextFactory.Create() as IDataContext;
        
        // Try to find existing manifest
        var existing = await context.Manifests
            .FirstOrDefaultAsync(m => m.ExternalId == externalId, ct);
        
        if (existing != null)
        {
            // Update ONLY scheduling-related fields. Preserve runtime state fields:
            // - LastSuccessfulRun, LastFailedRun, ConsecutiveFailures, etc.
            // Never delete and recreate - always update in place.
            existing.Name = typeof(TWorkflow).AssemblyQualifiedName!;
            existing.PropertyTypeName = input.GetType().AssemblyQualifiedName;
            existing.SetProperties(input);
            existing.IsEnabled = options.IsEnabled;
            existing.MaxRetries = options.MaxRetries;
            existing.TimeoutSeconds = options.Timeout.HasValue 
                ? (int)options.Timeout.Value.TotalSeconds 
                : null;
            ApplySchedule(existing, schedule);
            // Fields NOT touched: LastSuccessfulRun, LastFailedRun, ConsecutiveFailures, 
            // CreatedAt, etc. - these are runtime state managed by ManifestExecutorWorkflow
        }
        else
        {
            // Create new manifest
            var manifest = new Manifest
            {
                ExternalId = externalId,
                Name = typeof(TWorkflow).AssemblyQualifiedName!,
                PropertyTypeName = input.GetType().AssemblyQualifiedName,
                IsEnabled = options.IsEnabled,
                MaxRetries = options.MaxRetries,
                TimeoutSeconds = options.Timeout.HasValue 
                    ? (int)options.Timeout.Value.TotalSeconds 
                    : null,
            };
            manifest.SetProperties(input);
            ApplySchedule(manifest, schedule);
            
            await context.Track(manifest);
            existing = manifest;
        }
        
        await context.SaveChanges(ct);
        
        logger.LogInformation(
            "Scheduled workflow {Workflow} with ExternalId {ExternalId}", 
            typeof(TWorkflow).Name, 
            externalId);
        
        return existing;
    }
    
    public async Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        CancellationToken ct = default)
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        // Validate workflow is registered before starting transaction
        var inputType = typeof(TInput);
        if (!workflowRegistry.InputTypeToWorkflow.ContainsKey(inputType))
        {
            throw new InvalidOperationException(
                $"Workflow for input type '{inputType.Name}' is not registered in the WorkflowRegistry. " +
                $"Ensure the workflow assembly is included in AddEffectWorkflowBus().");
        }
        
        var results = new List<Manifest>();
        
        using var context = dataContextFactory.Create() as IDataContext;
        await using var transaction = await context.BeginTransaction();
        
        try
        {
            foreach (var source in sources)
            {
                var (externalId, input) = map(source);
                var options = new ManifestOptions();
                configure?.Invoke(source, options);
                
                var manifest = await UpsertManifestAsync<TWorkflow, TInput>(
                    context, externalId, input, schedule, options, ct);
                results.Add(manifest);
            }
            
            await context.SaveChanges(ct);
            await transaction.CommitAsync();
            
            logger.LogInformation(
                "Scheduled {Count} manifests for workflow {Workflow} in single transaction",
                results.Count,
                typeof(TWorkflow).Name);
            
            return results;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    private static void ApplySchedule(Manifest manifest, Schedule schedule)
    {
        manifest.ScheduleType = schedule.Type;
        manifest.CronExpression = schedule.CronExpression;
        manifest.IntervalSeconds = schedule.Interval.HasValue 
            ? (int)schedule.Interval.Value.TotalSeconds 
            : null;
    }
    
    // DisableAsync, EnableAsync, TriggerAsync implementations...
}
```

---

## File Structure

New files to create:

```
ChainSharp.Effect.Scheduler/
├── Services/
│   ├── ManifestScheduler/
│   │   ├── IManifestScheduler.cs          # Interface definition
│   │   └── ManifestScheduler.cs           # Implementation
│   └── Schedule/
│       ├── Schedule.cs                     # Schedule record
│       ├── Every.cs                        # Interval helpers
│       └── Cron.cs                         # Cron helpers
├── Configuration/
│   ├── ManifestOptions.cs                  # Options class
│   ├── PendingManifest.cs                  # Internal pending manifest
│   └── SchedulerConfigurationExtensions.cs # .Schedule() and .ScheduleMany() extensions
└── Extensions/
    └── (update existing to register IManifestScheduler)
```

---

## Migration Path

### Before (Current)

```csharp
static async Task SeedSampleManifestAsync(IServiceProvider services, string connectionString)
{
    using var scope = services.CreateScope();
    var dataContextFactory = scope.ServiceProvider.GetService<IDataContextProviderFactory>();

    if (dataContextFactory == null)
    {
        Console.WriteLine("Warning: DataContextFactory not available. Skipping manifest seeding.");
        return;
    }

    try
    {
        using var context = dataContextFactory.Create() as IDataContext;
        if (context == null) return;

        var existingManifest = context.Manifests
            .FirstOrDefault(m => m.ExternalId == "sample-hello-world");

        if (existingManifest == null)
        {
            var manifest = new Manifest
            {
                ExternalId = "sample-hello-world",
                Name = typeof(IHelloWorldWorkflow).AssemblyQualifiedName!,
                PropertyTypeName = typeof(HelloWorldInput).AssemblyQualifiedName,
                Properties = System.Text.Json.JsonSerializer.Serialize(new HelloWorldInput
                {
                    Name = "ChainSharp Scheduler"
                }),
                IsEnabled = true,
                ScheduleType = ScheduleType.Interval,
                IntervalSeconds = 60,
                MaxRetries = 3
            };

            context.Manifests.Add(manifest);
            await context.SaveChanges(CancellationToken.None);

            Console.WriteLine("Seeded sample 'HelloWorld' manifest (runs every 60 seconds)");
        }
    }
    catch (Exception ex)
    {
        // Error handling...
    }
}
```

### After (New API)

**Option A: Startup Configuration**

```csharp
builder.Services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(/* ... */)
        .Schedule<IHelloWorldWorkflow>(
            "sample-hello-world",
            new HelloWorldInput { Name = "ChainSharp Scheduler" },
            Every.Minutes(1))
    )
);
```

**Option B: Runtime via IManifestScheduler**

```csharp
static async Task SeedSampleManifestAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var scheduler = scope.ServiceProvider.GetRequiredService<IManifestScheduler>();

    await scheduler.ScheduleAsync<IHelloWorldWorkflow, HelloWorldInput>(
        "sample-hello-world",
        new HelloWorldInput { Name = "ChainSharp Scheduler" },
        Every.Minutes(1));
}
```

---

## Design Principles

1. **Type Safety**: Generic constraints ensure workflow and input types are compatible at compile time.

2. **Upsert Semantics**: All schedule methods use ExternalId for idempotent create-or-update behavior. Safe to call on every startup.

3. **LINQ Composability**: `ScheduleMany` accepts any `IEnumerable<TSource>`, allowing users to compose complex job sets using standard LINQ patterns they already know.

4. **Minimal API Surface**: Only two scheduling methods (`Schedule` and `ScheduleMany`). No Grid helpers or special overloads—users compose with LINQ.

5. **Hangfire-Inspired**: The API mirrors Hangfire's `RecurringJob.AddOrUpdate<TService>` pattern, making it familiar to .NET developers.

6. **Separation of Concerns**: 
   - `Schedule`/`Every`/`Cron` handle schedule definition
   - `ManifestOptions` handles job configuration
   - `IManifestScheduler` handles persistence and management

---

## Comparison to Hangfire

| Hangfire | ChainSharp |
|----------|------------|
| `RecurringJob.AddOrUpdate<IService>("id", s => s.Method(args), Cron.Daily)` | `scheduler.Schedule<IWorkflow>("id", new Input { ... }, Cron.Daily())` |
| `BackgroundJob.Enqueue<IService>(s => s.Method(args))` | `scheduler.TriggerAsync("id")` |
| Service + Method + Args (expression tree) | Workflow + Input (typed object) |

ChainSharp's approach is simpler because:
- No expression tree parsing required
- Input types are validated at compile time
- `IManifestProperties` makes the serialization contract explicit
- Upsert semantics built-in (Hangfire requires checking existence separately)

---

## Open Questions

1. ~~**Batch Persistence**: Should `ScheduleManyAsync` save all manifests in a single transaction, or one at a time?~~ **Resolved**: Single transaction with rollback on failure.

2. ~~**Async Configuration**: The startup `.Schedule()` extension is synchronous (deferred until `UseChainSharpScheduler`). Should we support async manifest generation during configuration?~~ **Resolved**: Not for now. All manifest seeding happens synchronously in `UseChainSharpScheduler`, within a single transaction for atomicity.

3. ~~**Validation**: Should we validate that `TWorkflow` is registered in the WorkflowBus during scheduling, or fail at runtime when the job executes?~~ **Resolved**: Yes, validate at scheduling time. The scheduler should check that `TWorkflow` is registered in the `WorkflowRegistry` and throw immediately if not. This catches configuration errors during startup rather than at 3am when the job tries to run.

4. ~~**Conflict Resolution**: When upserting, should we preserve certain fields from the existing manifest (e.g., `LastSuccessfulRun`)?~~ **Resolved**: Yes, preserve existing fields. Upsert should update only the fields provided by the scheduling call (schedule, input, options) and preserve runtime state fields like `LastSuccessfulRun`, `LastFailedRun`, `ConsecutiveFailures`, etc. Never delete and recreate rows.

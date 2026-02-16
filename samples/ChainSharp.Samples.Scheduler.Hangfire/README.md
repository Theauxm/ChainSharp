# ChainSharp.Samples.Scheduler.Hangfire

A sample application demonstrating how to use ChainSharp.Effect.Orchestration.Scheduler with Hangfire as the background task server.

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database running locally (or update connection string)
- Docker (optional, for running PostgreSQL)

## Quick Start with Docker

If you have Docker installed, you can start a PostgreSQL instance using the docker-compose file in the repository root:

```bash
cd /path/to/ChainSharp
docker-compose up -d
```

This starts PostgreSQL on `localhost:5432` with username `postgres` and password `postgres`.

## Running the Sample

1. Ensure PostgreSQL is running and accessible at the connection string in `appsettings.json`

2. Run the sample:
   ```bash
   dotnet run
   ```

3. Open the Hangfire Dashboard at http://localhost:5000/hangfire

## What This Sample Demonstrates

### Manifest-Based Scheduling

The sample seeds a "HelloWorld" manifest that runs every 60 seconds. The manifest system allows:

- **Declarative job definitions** - Jobs are defined as manifests with scheduling rules
- **Automatic execution tracking** - Each execution creates a Metadata record
- **Retry policies** - Failed jobs are automatically retried up to `MaxRetries` times
- **Dead letter queue** - Jobs that exceed retry limits are moved to dead letter for manual intervention

### Components

| Component | Purpose |
|-----------|---------|
| `HelloWorldWorkflow` | Simple EffectWorkflow that logs a greeting |
| `HelloWorldInput` | Workflow input implementing `IManifestProperties` |
| `LogGreetingStep` | Step that performs the greeting logic |
| `ManifestManagerWorkflow` | Built-in workflow that polls for scheduled jobs |
| `TaskServerExecutorWorkflow` | Built-in workflow that executes individual jobs |

### Hangfire Integration

The sample configures Hangfire with:

- **PostgreSQL storage** - Jobs are persisted in the same database as ChainSharp metadata
- **Recurring poll job** - The ManifestManager runs on a configurable interval
- **Dashboard** - Web UI for monitoring jobs at `/hangfire`

## Configuration

Edit `appsettings.json` to customize:

```json
{
  "ConnectionStrings": {
    "ChainSharpDatabase": "your-connection-string"
  },
  "Scheduler": {
    "PollingIntervalSeconds": 30,
    "MaxActiveJobs": 50,
    "DefaultMaxRetries": 3
  }
}
```

## Creating Your Own Scheduled Workflows

1. **Create a workflow input** implementing `IManifestProperties`:
   ```csharp
   public record MyJobInput : IManifestProperties
   {
       public string SomeProperty { get; init; }
   }
   ```

2. **Create a workflow**:
   ```csharp
   public class MyJobWorkflow : EffectWorkflow<MyJobInput, Unit>, IMyJobWorkflow
   {
       protected override async Task<Either<Exception, Unit>> RunInternal(MyJobInput input) =>
           Activate(input)
               .Chain<MyStep>()
               .Resolve();
   }
   
   public interface IMyJobWorkflow : IEffectWorkflow<MyJobInput, Unit>;
   ```

3. **Create a manifest** (via API, database, or seeding):
   ```csharp
   var manifest = new Manifest
   {
       ExternalId = "my-job",
       Name = typeof(IMyJobWorkflow).AssemblyQualifiedName!,
       PropertyTypeName = typeof(MyJobInput).AssemblyQualifiedName,
       Properties = JsonSerializer.Serialize(new MyJobInput { SomeProperty = "value" }),
       IsEnabled = true,
       ScheduleType = ScheduleType.Cron,
       CronExpression = "0 0 * * *", // Daily at midnight
       MaxRetries = 3
   };
   ```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Hangfire Server                               │
│         Runs ManifestManagerWorkflow on schedule                │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestManagerWorkflow                         │
│  LoadManifests → ReapFailedJobs → DetermineJobs → EnqueueJobs  │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ Enqueues
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TaskServerExecutorWorkflow                        │
│             Executes individual workflow from Manifest          │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Your Workflow (HelloWorldWorkflow)             │
│                    Your business logic here                      │
└─────────────────────────────────────────────────────────────────┘
```

## Troubleshooting

### Database connection errors

Ensure PostgreSQL is running and the connection string is correct. The sample will automatically run migrations on startup.

### Hangfire tables not created

Hangfire creates its own tables on first run. If you see errors about missing tables, ensure the database user has CREATE TABLE permissions.

### Jobs not running

1. Check the Hangfire Dashboard for job status
2. Verify the manifest's `IsEnabled` is `true`
3. Check the manifest's `ScheduleType` and scheduling configuration
4. Look at the application logs for errors

using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.JobDispatcherPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestManagerPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using ChainSharp.Effect.Orchestration.Scheduler.Services.MetadataCleanupPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Services.SchedulerStartupService;
using ChainSharp.Effect.Orchestration.Scheduler.Utilities;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

/// <summary>
/// Fluent builder for configuring the ChainSharp scheduler.
/// </summary>
/// <remarks>
/// This builder allows configuring the scheduler as part of the ChainSharp effects setup:
/// <code>
/// services.AddChainSharpEffects(options => options
///     .AddEffectWorkflowBus(assemblies)
///     .AddPostgresEffect(connectionString)
///     .AddScheduler(scheduler => scheduler
///         .PollingInterval(TimeSpan.FromSeconds(30))
///         .MaxActiveJobs(100)
///         .UseHangfire(config => config.UsePostgreSqlStorage(...))
///     )
/// );
/// </code>
/// </remarks>
public partial class SchedulerConfigurationBuilder
{
    private readonly ChainSharpEffectConfigurationBuilder _parentBuilder;
    private readonly SchedulerConfiguration _configuration = new();
    private Action<IServiceCollection>? _taskServerRegistration;
    private string? _rootScheduledExternalId;
    private string? _lastScheduledExternalId;

    // Dependency graph tracking for cycle detection at build time
    private readonly Dictionary<string, string> _externalIdToGroupId = new();
    private readonly List<(string ParentExternalId, string ChildExternalId)> _dependencyEdges = [];

    /// <summary>
    /// Creates a new scheduler configuration builder.
    /// </summary>
    /// <param name="parentBuilder">The parent ChainSharp effect configuration builder</param>
    public SchedulerConfigurationBuilder(ChainSharpEffectConfigurationBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }

    /// <summary>
    /// Gets the service collection for registering services.
    /// </summary>
    public IServiceCollection ServiceCollection => _parentBuilder.ServiceCollection;

    /// <summary>
    /// Builds the scheduler configuration and registers all services.
    /// </summary>
    /// <returns>The parent builder for continued chaining</returns>
    internal ChainSharpEffectConfigurationBuilder Build()
    {
        ValidateNoCyclicGroupDependencies();

        // Exclude internal scheduler workflows from MaxActiveJobs count
        foreach (var name in AdminWorkflows.FullNames)
            _configuration.ExcludedWorkflowTypeNames.Add(name);

        // Register the configuration
        _parentBuilder.ServiceCollection.AddSingleton(_configuration);

        // Register IManifestScheduler
        _parentBuilder.ServiceCollection.AddScoped<IManifestScheduler, ManifestScheduler>();

        // Register JobDispatcher workflow (must use AddScopedChainSharpWorkflow for property injection)
        _parentBuilder.ServiceCollection.AddScopedChainSharpWorkflow<
            IJobDispatcherWorkflow,
            JobDispatcherWorkflow
        >();

        // Register task server if configured
        _taskServerRegistration?.Invoke(_parentBuilder.ServiceCollection);

        // Registration order matters: .NET starts IHostedService instances sequentially in registration order.
        // SchedulerStartupService must complete before the polling services begin.
        _parentBuilder.ServiceCollection.AddHostedService<SchedulerStartupService>();
        _parentBuilder.ServiceCollection.AddHostedService<ManifestManagerPollingService>();
        _parentBuilder.ServiceCollection.AddHostedService<JobDispatcherPollingService>();

        // Register the metadata cleanup service if configured
        if (_configuration.MetadataCleanup is not null)
            _parentBuilder.ServiceCollection.AddHostedService<MetadataCleanupPollingService>();

        return _parentBuilder;
    }

    /// <summary>
    /// Validates that the manifest group dependency graph is a DAG (no circular dependencies).
    /// </summary>
    private void ValidateNoCyclicGroupDependencies()
    {
        if (_dependencyEdges.Count == 0)
            return;

        // Derive group-level edges from manifest-level edges
        var groupNodes = new System.Collections.Generic.HashSet<string>(
            _externalIdToGroupId.Values
        );
        var groupEdges = _dependencyEdges
            .Select(e =>
            {
                _externalIdToGroupId.TryGetValue(e.ParentExternalId, out var fromGroup);
                _externalIdToGroupId.TryGetValue(e.ChildExternalId, out var toGroup);
                return (From: fromGroup, To: toGroup);
            })
            .Where(e => e.From is not null && e.To is not null && e.From != e.To)
            .Select(e => (e.From!, e.To!))
            .Distinct()
            .ToList();

        if (groupEdges.Count == 0)
            return;

        var result = DagValidator.TopologicalSort(groupNodes, groupEdges);

        if (!result.IsAcyclic)
        {
            var cycleGroups = string.Join(", ", result.CycleMembers.Order());
            throw new InvalidOperationException(
                $"Circular dependency detected among manifest groups: [{cycleGroups}]. "
                    + "Manifest groups must form a directed acyclic graph (DAG)."
            );
        }
    }
}

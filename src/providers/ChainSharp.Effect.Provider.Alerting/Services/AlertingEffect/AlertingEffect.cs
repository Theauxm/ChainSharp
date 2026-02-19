using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;
using ChainSharp.Effect.Services.EffectProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Provider.Alerting.Services.AlertingEffect;

/// <summary>
/// Effect provider that sends alerts when workflow failure conditions are met.
/// </summary>
/// <remarks>
/// AlertingEffect implements IEffectProvider and reacts to the OnError hook.
/// When a workflow implementing IAlertingWorkflow fails, this provider:
///
/// 1. Retrieves the cached alert configuration for the workflow
/// 2. Checks debounce state (if enabled)
/// 3. For MinimumFailures == 1: Sends alert immediately (no DB query)
/// 4. For MinimumFailures > 1: Queries DB for historical failures and evaluates conditions
/// 5. If conditions are met: Sends alerts to all registered IAlertSender implementations
/// 6. Sets debounce cooldown (if enabled)
///
/// Performance optimizations:
/// - Single DB query for historical data (when needed)
/// - In-memory filtering after query
/// - Skips DB query entirely when MinimumFailures == 1
/// - Debounce state cached in IMemoryCache (no DB interaction)
/// </remarks>
public class AlertingEffect : IEffectProvider
{
    private readonly IDataContext? _dataContext;
    private readonly IMemoryCache _cache;
    private readonly IEnumerable<IAlertSender> _alertSenders;
    private readonly IAlertConfigurationRegistry _configRegistry;
    private readonly AlertingOptionsBuilder _options;
    private readonly ILogger<AlertingEffect> _logger;

    /// <summary>
    /// Initializes a new instance of the AlertingEffect class.
    /// </summary>
    /// <param name="dataContext">Database context for querying historical failures (optional)</param>
    /// <param name="cache">Memory cache for debounce state</param>
    /// <param name="alertSenders">All registered IAlertSender implementations</param>
    /// <param name="configRegistry">Registry containing cached alert configurations</param>
    /// <param name="options">Alerting options including debounce settings</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <remarks>
    /// The dataContext parameter is optional to allow the alerting system to work
    /// without a database provider (e.g., with InMemory provider or no data provider).
    /// When dataContext is null, only MinimumFailures == 1 configurations will work.
    /// </remarks>
    public AlertingEffect(
        IDataContext? dataContext,
        IMemoryCache cache,
        IEnumerable<IAlertSender> alertSenders,
        IAlertConfigurationRegistry configRegistry,
        AlertingOptionsBuilder options,
        ILogger<AlertingEffect> _logger
    )
    {
        _dataContext = dataContext;
        _cache = cache;
        _alertSenders = alertSenders;
        _configRegistry = configRegistry;
        _options = options;
        this._logger = _logger;
    }

    /// <inheritdoc />
    public Task SaveChanges(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Track(IModel model) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Update(IModel model) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task OnError(
        Metadata metadata,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // Get alert configuration for this workflow
        var config = _configRegistry.GetConfiguration(metadata.Name);
        if (config == null)
        {
            // Not an alerting workflow - nothing to do
            return;
        }

        _logger.LogDebug("Evaluating alert conditions for workflow {Workflow}", metadata.Name);

        // Check debounce if enabled
        if (_options.DebounceEnabled && _options.CooldownPeriod.HasValue)
        {
            var cacheKey = $"alert_cooldown_{metadata.Name}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogDebug(
                    "Alert suppressed for {Workflow} - cooldown period active",
                    metadata.Name
                );
                return;
            }
        }

        // OPTIMIZATION: If MinimumFailures == 1, skip DB query
        if (config.MinimumFailures == 1)
        {
            // Check if this single failure matches filters
            if (MatchesFilters(metadata, config))
            {
                var context = BuildSingleFailureContext(metadata, config);
                await SendAlerts(context, cancellationToken);
            }
            return;
        }

        // For MinimumFailures > 1, query database for historical context
        if (_dataContext == null)
        {
            _logger.LogWarning(
                "Alert configuration for {Workflow} requires database queries "
                    + "(MinimumFailures > 1) but no IDataContext is available. "
                    + "Alert will not be sent. Consider configuring a data provider "
                    + "or setting MinimumFailures to 1.",
                metadata.Name
            );
            return;
        }

        // Single database query to get all relevant metadata in the time window
        var windowStart = DateTime.UtcNow - config.TimeWindow;
        var historicalMetadata = await _dataContext
            .Metadatas.Where(
                m =>
                    m.Name == metadata.Name
                    && m.StartTime >= windowStart
                    && m.WorkflowState != WorkflowState.Pending
                    && m.WorkflowState != WorkflowState.InProgress
            )
            .OrderByDescending(m => m.StartTime)
            .ToListAsync(cancellationToken);

        // Apply filters in-memory to get matching failures
        var failedExecutions = historicalMetadata
            .Where(m => m.WorkflowState == WorkflowState.Failed)
            .Where(m => MatchesFilters(m, config))
            .ToList();

        // Check if alert conditions are met
        if (failedExecutions.Count < config.MinimumFailures)
        {
            _logger.LogDebug(
                "Alert conditions not met for {Workflow}: {Count} failures < {MinFailures} required",
                metadata.Name,
                failedExecutions.Count,
                config.MinimumFailures
            );
            return;
        }

        // Build comprehensive alert context and send alerts
        var alertContext = BuildAlertContext(
            metadata,
            failedExecutions,
            historicalMetadata,
            config
        );
        await SendAlerts(alertContext, cancellationToken);
    }

    /// <summary>
    /// Checks if a metadata record matches all configured filters.
    /// </summary>
    /// <param name="metadata">The metadata to check</param>
    /// <param name="config">The alert configuration containing filters</param>
    /// <returns>True if the metadata matches all filters, false otherwise</returns>
    private bool MatchesFilters(Metadata metadata, AlertConfiguration config)
    {
        // Check exception type filter (OR logic - match ANY)
        if (config.ExceptionTypes.Count > 0 && !string.IsNullOrEmpty(metadata.FailureException))
        {
            var matches = config.ExceptionTypes.Any(
                et =>
                    et.Name == metadata.FailureException || et.FullName == metadata.FailureException
            );
            if (!matches)
                return false;
        }

        // Check step name filters (OR logic - match ANY)
        if (config.StepFilters.Count > 0 && !string.IsNullOrEmpty(metadata.FailureStep))
        {
            var matches = config.StepFilters.Any(filter =>
            {
                if (filter.exactMatch != null)
                    return metadata.FailureStep == filter.exactMatch;
                if (filter.predicate != null)
                    return filter.predicate(metadata.FailureStep);
                return false;
            });
            if (!matches)
                return false;
        }

        // Check custom filters (AND logic - ALL must pass)
        foreach (var customFilter in config.CustomFilters)
        {
            if (!customFilter(metadata))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Builds an AlertContext for a single failure (MinimumFailures == 1 optimization).
    /// </summary>
    private AlertContext BuildSingleFailureContext(
        Metadata triggerMetadata,
        AlertConfiguration config
    )
    {
        return new AlertContext
        {
            WorkflowName = triggerMetadata.Name,
            TriggerMetadata = triggerMetadata,
            FailureCount = 1,
            TimeWindow = TimeSpan.Zero,
            Configuration = config,
            FailedExecutions = new[] { triggerMetadata },
            TotalExecutions = 1,
            FirstFailureTime = triggerMetadata.StartTime,
            LastSuccessTime = null,
            ExceptionFrequency = new Dictionary<string, int>
            {
                [triggerMetadata.FailureException ?? "Unknown"] = 1
            },
            FailedStepFrequency = new Dictionary<string, int>
            {
                [triggerMetadata.FailureStep ?? "Unknown"] = 1
            },
            FailedInputs = !string.IsNullOrEmpty(triggerMetadata.Input)
                ? new[] { triggerMetadata.Input }
                : Array.Empty<string>()
        };
    }

    /// <summary>
    /// Builds a comprehensive AlertContext from historical metadata.
    /// </summary>
    private AlertContext BuildAlertContext(
        Metadata triggerMetadata,
        List<Metadata> failedExecutions,
        List<Metadata> allExecutions,
        AlertConfiguration config
    )
    {
        // Calculate exception frequency
        var exceptionFrequency = failedExecutions
            .GroupBy(m => m.FailureException ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate failed step frequency
        var stepFrequency = failedExecutions
            .GroupBy(m => m.FailureStep ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        // Get failed inputs (exclude nulls and empty strings)
        var failedInputs = failedExecutions
            .Select(m => m.Input)
            .Where(input => !string.IsNullOrEmpty(input))
            .ToList();

        // Find last successful execution
        var lastSuccess = allExecutions
            .Where(m => m.WorkflowState == WorkflowState.Completed)
            .OrderByDescending(m => m.EndTime)
            .FirstOrDefault();

        return new AlertContext
        {
            WorkflowName = triggerMetadata.Name,
            TriggerMetadata = triggerMetadata,
            FailureCount = failedExecutions.Count,
            TimeWindow = config.TimeWindow,
            Configuration = config,
            FailedExecutions = failedExecutions.AsReadOnly(),
            TotalExecutions = allExecutions.Count,
            FirstFailureTime = failedExecutions.Min(m => m.StartTime),
            LastSuccessTime = lastSuccess?.EndTime,
            ExceptionFrequency = exceptionFrequency,
            FailedStepFrequency = stepFrequency,
            FailedInputs = failedInputs.AsReadOnly()!
        };
    }

    /// <summary>
    /// Sends alerts to all registered IAlertSender implementations.
    /// </summary>
    private async Task SendAlerts(AlertContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sending alert for workflow {Workflow}: {Count} failures met alert conditions",
            context.WorkflowName,
            context.FailureCount
        );

        // Send to all registered senders
        foreach (var sender in _alertSenders)
        {
            try
            {
                await sender.SendAlertAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but continue to other senders
                _logger.LogError(
                    ex,
                    "Alert sender {Sender} failed for workflow {Workflow}. "
                        + "Error will be logged but other senders will still be notified.",
                    sender.GetType().Name,
                    context.WorkflowName
                );
            }
        }

        // Set debounce cooldown if enabled
        if (_options.DebounceEnabled && _options.CooldownPeriod.HasValue)
        {
            var cacheKey = $"alert_cooldown_{context.WorkflowName}";
            _cache.Set(cacheKey, true, _options.CooldownPeriod.Value);

            _logger.LogDebug(
                "Set alert cooldown for {Workflow}: {Duration}",
                context.WorkflowName,
                _options.CooldownPeriod.Value
            );
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No resources to dispose
    }
}

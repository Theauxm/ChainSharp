using System.Text.RegularExpressions;
using System.Threading.Channels;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLoggingProvider : IDataContextLoggingProvider
{
    private readonly IDataContextProviderFactory _dbContextFactory;
    private readonly IDataContextLoggingProviderConfiguration _configuration;
    private readonly HashSet<string> _exactBlacklist = [];
    private readonly List<Regex> _wildcardBlacklist = [];

    private readonly Channel<Effect.Models.Log.Log> _logChannel =
        Channel.CreateBounded<Effect.Models.Log.Log>(
            new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            }
        );

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;

    public DataContextLoggingProvider(
        IDataContextProviderFactory dbContextFactory,
        IDataContextLoggingProviderConfiguration configuration
    )
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;

        foreach (var pattern in configuration.Blacklist)
        {
            if (pattern.Contains('*'))
            {
                // Convert wildcard to regex, escaping dots and replacing '*' with '.*'
                var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
                _wildcardBlacklist.Add(new Regex(regexPattern, RegexOptions.Compiled));
            }
            else
                _exactBlacklist.Add(pattern);
        }

        _flushTask = Task.Run(() => FlushLoopAsync(_cts.Token));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DataContextLogger(
            _logChannel.Writer,
            categoryName,
            _configuration.MinimumLogLevel,
            _exactBlacklist,
            _wildcardBlacklist
        );
    }

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        var batch = new List<Effect.Models.Log.Log>(64);

        try
        {
            while (await _logChannel.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();

                while (batch.Count < 64 && _logChannel.Reader.TryRead(out var log))
                    batch.Add(log);

                if (batch.Count == 0)
                    continue;

                try
                {
                    using var dataContext = await _dbContextFactory.CreateDbContextAsync(ct);

                    foreach (var log in batch)
                        await dataContext.Track(log);

                    await dataContext.SaveChanges(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Drop the batch on failure to prevent infinite retry loops.
                    // Logging failures should not crash the application.
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }

    public void Dispose()
    {
        _logChannel.Writer.TryComplete();
        _cts.Cancel();

        // Best-effort wait for the flush loop to drain
        _flushTask.Wait(TimeSpan.FromSeconds(5));

        _cts.Dispose();
    }
}

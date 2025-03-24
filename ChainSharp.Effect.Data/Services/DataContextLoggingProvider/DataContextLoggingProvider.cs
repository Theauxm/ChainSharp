using System.Text.RegularExpressions;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLoggingProvider : IDataContextLoggingProvider
{
    private readonly IDataContextProviderFactory _dataContextProviderFactory;
    private readonly IDataContextLoggingProviderConfiguration _configuration;
    private readonly HashSet<string> _exactBlacklist = [];
    private readonly List<Regex> _wildcardBlacklist = [];

    public DataContextLoggingProvider(
        IDataContextProviderFactory dataContextProviderFactory,
        IDataContextLoggingProviderConfiguration configuration
    )
    {
        _dataContextProviderFactory = dataContextProviderFactory;
        _configuration = configuration;
        var blacklistPatterns = configuration.Blacklist;

        foreach (var pattern in blacklistPatterns)
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
    }

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        return new DataContextLogger(
            _dataContextProviderFactory,
            _configuration.EvaluationStrategy,
            _configuration.MinimumLogLevel,
            _exactBlacklist,
            _wildcardBlacklist,
            categoryName
        );
    }
}

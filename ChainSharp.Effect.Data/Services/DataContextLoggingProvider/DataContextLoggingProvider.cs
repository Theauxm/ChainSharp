using System.Text.RegularExpressions;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLoggingProvider : IDataContextLoggingProvider
{
    private readonly IDataContextProviderFactory _dbContextFactory;
    private readonly IDataContextLoggingProviderConfiguration _configuration;
    private readonly HashSet<string> _exactBlacklist = [];
    private readonly List<Regex> _wildcardBlacklist = [];

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
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DataContextLogger(
            _dbContextFactory,
            categoryName,
            _configuration.MinimumLogLevel,
            _exactBlacklist,
            _wildcardBlacklist
        );
    }

    public void Dispose() { }
}

using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddChainSharpLogging(this IServiceCollection services)
    {
        return services;
    }
}

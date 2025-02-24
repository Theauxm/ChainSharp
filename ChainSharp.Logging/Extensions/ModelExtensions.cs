using ChainSharp.Logging.Services.LoggingProviderContext;

namespace ChainSharp.Logging.Extensions;

public static class ModelExtensions
{
    internal static T TrackedIn<T>(this T entity, ILoggingProviderContext db)
        where T : class
    {
        db.Raw.Add(entity);
        return entity;
    }
}

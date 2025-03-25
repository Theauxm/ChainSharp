using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.EffectProvider;

public interface IEffectProvider : IDisposable
{
    public Task SaveChanges(CancellationToken cancellationToken);

    public Task Track(IModel model);
}

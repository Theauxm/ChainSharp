using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.EffectRunner;

public interface IEffectRunner : IDisposable
{
    public Task SaveChanges(CancellationToken cancellationToken);

    public Task Track(IModel model);
}

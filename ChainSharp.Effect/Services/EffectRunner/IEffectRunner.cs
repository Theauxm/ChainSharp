using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.EffectRunner;

public interface IEffectRunner : IDisposable
{
    public Task SaveChanges();

    public Task Track(IModel model);
}

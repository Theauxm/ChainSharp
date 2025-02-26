using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.EffectProvider;

public interface IEffectProvider : IDisposable
{
    public Task SaveChanges();

    public Task Track(IModel model);
}

using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.Effect;

public interface IEffect : IDisposable
{
    public Task SaveChanges();

    public Task Track(IModel model);
}

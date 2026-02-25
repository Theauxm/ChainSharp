using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.StepProvider.Progress.Services.CancellationCheckProvider;

public class CancellationCheckProvider(IDataContextProviderFactory dataContextFactory)
    : ICancellationCheckProvider
{
    public async Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        if (effectWorkflow.Metadata is null)
            return;

        await using var context = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var cancelRequested = await context
            .Metadatas.Where(m => m.Id == effectWorkflow.Metadata.Id)
            .Select(m => m.CancellationRequested)
            .FirstOrDefaultAsync(cancellationToken);

        if (cancelRequested)
            throw new OperationCanceledException("Workflow cancellation requested via dashboard.");
    }

    public Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public void Dispose() { }
}

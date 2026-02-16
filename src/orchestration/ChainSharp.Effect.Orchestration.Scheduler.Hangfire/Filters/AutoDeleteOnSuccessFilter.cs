using Hangfire.Common;
using Hangfire.States;

namespace ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Filters;

/// <summary>
/// Hangfire filter that automatically deletes jobs upon successful completion.
/// </summary>
/// <remarks>
/// ChainSharp.Effect.Orchestration.Scheduler tracks all execution state in its own Metadata
/// and DeadLetter tables, so retaining succeeded (or failed) jobs in Hangfire's storage is
/// unnecessary. This filter transitions completed jobs directly to the Deleted state,
/// preventing unbounded growth of Hangfire's internal job tables.
/// </remarks>
public class AutoDeleteOnSuccessFilter : JobFilterAttribute, IElectStateFilter
{
    /// <inheritdoc />
    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is SucceededState or FailedState)
        {
            context.CandidateState = new DeletedState();
        }
    }
}

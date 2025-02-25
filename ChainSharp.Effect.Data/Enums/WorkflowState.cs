using NpgsqlTypes;

namespace ChainSharp.Effect.Data.Enums;

[PgName("workflow_state")]
public enum WorkflowState
{
    [PgName("pending")]
    Pending,

    [PgName("completed")]
    Completed,

    [PgName("failed")]
    Failed,

    [PgName("in_progress")]
    InProgress,
}

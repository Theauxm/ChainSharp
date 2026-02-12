using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport;

public interface IExtractImportWorkflow : IEffectWorkflow<ExtractImportInput, Unit>;

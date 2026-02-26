using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.ExtractImport;

public interface IExtractImportWorkflow : IServiceTrain<ExtractImportInput, Unit>;

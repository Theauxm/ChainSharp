namespace ChainSharp.Logging.Services.WorkflowLogger;

public interface IWorkflowLogger
{
    public void Info(string message);

    public void Debug(string message);

    public void Warning(string message);

    public void Error(string message);

    public void Error(string message, Exception exception);
}

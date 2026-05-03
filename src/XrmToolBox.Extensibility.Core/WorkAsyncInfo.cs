namespace XrmToolBox.Extensibility;

public sealed class WorkAsyncInfo
{
    public string Message { get; set; } = string.Empty;
    public Action<DoWorkEventArgs>? Work { get; set; }
    public Action<RunWorkerCompletedEventArgs>? PostWorkCallBack { get; set; }
    public Action<int>? ProgressChanged { get; set; }
    public object? AsyncArgument { get; set; }
}

public sealed class DoWorkEventArgs(object? argument)
{
    public object? Argument { get; } = argument;
    public object? Result { get; set; }
    public bool Cancel { get; set; }
}

public sealed class RunWorkerCompletedEventArgs(object? result, Exception? error, bool cancelled)
{
    public object? Result { get; } = result;
    public Exception? Error { get; } = error;
    public bool Cancelled { get; } = cancelled;
}

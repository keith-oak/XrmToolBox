namespace XrmToolBox.Extensibility.Interfaces;

public sealed class StatusBarMessageEventArgs : EventArgs
{
    public string? Message { get; }
    public int? Progress { get; }

    public StatusBarMessageEventArgs(string? message) { Message = message; }
    public StatusBarMessageEventArgs(int progress) { Progress = progress; }
    public StatusBarMessageEventArgs(string? message, int progress) { Message = message; Progress = progress; }
}

public interface IStatusBarMessenger
{
    event EventHandler<StatusBarMessageEventArgs>? SendMessageToStatusBar;
}
